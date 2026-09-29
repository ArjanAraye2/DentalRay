using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using ReSiRai.Api.Data;
using ReSiRai.Api.Models;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// AI consultation ("consultation over the patient's structured state").
//
// The patient's condition - demographics, reason for visit and every recorded
// clinical factor with its unit, reference range and trend - is sent as standard,
// sourced numbers. The model answers a ranked differential with reasons, red
// flags and suggested workup.
//
// This is decision SUPPORT: nothing is written to the patient record, the output
// is a draft for the doctor, and every call is logged (model, coverage, outcome)
// so the consultation stays auditable.
[ApiController]
[Route("api/ai/consult")]
public sealed class AiConsultController : ControllerBase
{
    private readonly ReSiRaiDbContext _db;
    private readonly StudyAccessService _studyAccess;
    private readonly AiClient _ai;
    private readonly AppEventLogger _events;

    public AiConsultController(ReSiRaiDbContext db, StudyAccessService studyAccess,
        AiClient ai, AppEventLogger events)
    {
        _db = db;
        _studyAccess = studyAccess;
        _ai = ai;
        _events = events;
    }

    public sealed class ConsultRequest
    {
        public int StudyID { get; set; }

        /// <summary>SuperAdmin-only override, used by the admin test mode.</summary>
        public int? SpecialtyID { get; set; }
    }

    /// <summary>One dictionary row of the visit's specialty, flattened for the prompt.</summary>
    private sealed record ConsultFactor(int FactorID, string FactorCode, string NameEn, string? NameFa,
        string? UnitUCUM, decimal? RefLow, decimal? RefHigh, string? RefText, byte DataType, bool IsRequired);

    [HttpPost]
    public async Task<IActionResult> Consult(ConsultRequest request, CancellationToken cancellationToken)
    {
        if (request.StudyID <= 0) return BadRequest(new { success = false, message = "مراجعه مشخص نیست." });

        var accessible = _studyAccess.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User).Select(x => x.StudyID);
        bool canAccess = StudyAccessService.IsSuperAdmin(User) || accessible.Contains(request.StudyID);
        if (!canAccess) return NotFound(new { success = false, message = "مراجعه پیدا نشد." });

        var study = await _db.RadiologyStudies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudyID == request.StudyID, cancellationToken);
        if (study is null) return NotFound(new { success = false, message = "مراجعه پیدا نشد." });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        var patient = await _db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PatientID == study.PatientID, cancellationToken);

        // Factor set of the visit's doctor; internal medicine while a specialty is
        // missing (phase 1 default).
        int? specialtyID = null;
        if (study.DentistStaffID.HasValue)
            specialtyID = await _db.Staff.AsNoTracking()
                .Where(x => x.StaffID == study.DentistStaffID.Value)
                .Select(x => x.SpecialtyID)
                .FirstOrDefaultAsync(cancellationToken);
        specialtyID ??= await _db.DentalSpecialties.AsNoTracking()
            .Where(x => x.SpecialtyName == "بیماری‌های داخلی")
            .Select(x => x.SpecialtyID)
            .FirstOrDefaultAsync(cancellationToken);

        // The admin test mode may pick a specialty explicitly, so the whole flow
        // can be tried before any doctor is registered. Only SuperAdmins may.
        if (StudyAccessService.IsSuperAdmin(User) && request.SpecialtyID.HasValue)
            specialtyID = request.SpecialtyID;

        var defs = await (
            from f in _db.ClinicalFactors.AsNoTracking()
            join s in _db.SpecialtyFactorSets.AsNoTracking() on f.FactorID equals s.FactorID
            where s.SpecialtyID == specialtyID && f.IsActive
            orderby s.IsCommon descending, s.SortOrder
            select new ConsultFactor(f.FactorID, f.FactorCode, f.NameEn, f.NameFa,
                f.UnitUCUM, f.RefLow, f.RefHigh, f.RefText, f.DataType, s.IsRequired)
        ).ToListAsync(cancellationToken);

        var values = await _db.StudyFactorValues.AsNoTracking()
            .Where(x => x.StudyID == request.StudyID)
            .OrderByDescending(x => x.ObservedAt)
            .ToListAsync(cancellationToken);

        var prompt = BuildPrompt(patient, study, defs, values, out int recorded, out int requiredMissing);
        int coverage = defs.Count == 0 ? 100 : recorded * 100 / defs.Count;

        var stopwatch = Stopwatch.StartNew();
        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(prompt, Array.Empty<(string, byte[])>(), cancellationToken);
        }
        catch (AiException e)
        {
            stopwatch.Stop();
            await _events.LogAsync("ai.consult", "error",
                $"study={request.StudyID} model={_ai.Model} detail={e.Detail}",
                (int)stopwatch.ElapsedMilliseconds, GetCurrentUserID(), User.Identity?.Name);
            return StatusCode(e.HttpStatus, new { success = false, message = e.UserMessage, detail = e.Detail });
        }
        stopwatch.Stop();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            await _events.LogAsync("ai.consult", "bad-json", $"study={request.StudyID}",
                (int)stopwatch.ElapsedMilliseconds, GetCurrentUserID(), User.Identity?.Name);
            return StatusCode(502, new { success = false, message = "پاسخ مدل قابل خواندن نبود." });
        }

        await _events.LogAsync("ai.consult", "ok",
            $"study={request.StudyID} model={_ai.Model} coverage={coverage}% requiredMissing={requiredMissing}",
            (int)stopwatch.ElapsedMilliseconds, GetCurrentUserID(), User.Identity?.Name);

        using (doc)
        {
            return Ok(new
            {
                success = true,
                studyID = request.StudyID,
                model = _ai.Model,
                coverage,
                requiredMissing,
                consultation = doc.RootElement.Clone()
            });
        }
    }

    /// <summary>
    /// Builds the consultation input: the patient's state as standard, sourced
    /// numbers. Every factor line carries its unit and reference range, and the
    /// trend of earlier values, so the model reasons over evidence, not bare
    /// numbers. Unrecorded factors are listed as missing instead of being assumed
    /// normal - the rule that keeps the answer honest.
    /// </summary>
    private static string BuildPrompt(Patient? patient, RadiologyStudy study,
        List<ConsultFactor> defs, List<StudyFactorValue> values, out int recorded, out int requiredMissing)
    {
        recorded = 0;
        requiredMissing = 0;

        int? age = patient?.BirthDate is { } bd ? (int)((DateTime.Now - bd).TotalDays / 365.25) : null;
        string sex = patient?.Gender == 1 ? "male" : patient?.Gender == 2 ? "female" : "unknown";

        var byFactor = new Dictionary<int, List<StudyFactorValue>>();
        foreach (var v in values)
        {
            if (!byFactor.TryGetValue(v.FactorID, out var list)) byFactor[v.FactorID] = list = new();
            list.Add(v);
        }

        var lines = new List<string>();
        var missing = new List<string>();
        foreach (var def in defs)
        {
            if (!byFactor.TryGetValue(def.FactorID, out var list) || list.Count == 0)
            {
                missing.Add(def.NameEn);
                if (def.IsRequired) requiredMissing++;
                continue;
            }
            recorded++;

            var ordered = list.OrderByDescending(x => x.ObservedAt).Take(3).ToList();
            string current = FormatValue(ordered[0]);
            string trend = ordered.Count > 1
                ? "; previous: " + string.Join(", ", ordered.Skip(1).Select(FormatValue))
                : "";
            string reference = def.RefText != null
                ? def.RefText
                : (def.RefLow != null || def.RefHigh != null
                    ? $"reference {def.RefLow?.ToString() ?? "-"} to {def.RefHigh?.ToString() ?? "-"}"
                    : "no reference");
            lines.Add($"- {def.NameEn} ({def.FactorCode}) = {current}{trend} [{reference}, unit {def.UnitUCUM ?? "-"}]");
        }

        return $$"""
You are a clinical decision support assistant for an internal medicine clinic.
You receive the patient's structured state: demographics, the reason for the visit
and clinical factors expressed as standard, sourced numbers (unit + reference range
+ earlier values). Treat only values that are present; anything listed under
"notRecorded" is UNKNOWN, never normal.

Patient: age {{age?.ToString() ?? "unknown"}}, sex {{sex}}
Reason for visit: {{study.Description ?? "not recorded"}}
Doctor's notes: {{study.Report ?? "none"}}

Factors:
{{(lines.Count > 0 ? string.Join("\n", lines) : "none recorded")}}

notRecorded:
{{(missing.Count > 0 ? string.Join(", ", missing) : "none")}}

Rules:
- This is decision support for a physician; never present a diagnosis as certain.
- Base every probability only on the given values and say which values drove it.
- If the data is not enough, set notEnoughData to true and explain what is missing.
- Suggest workup that would change the decision, not a full textbook list.
- Flag urgent red flags separately.
Return ONLY valid JSON with this exact shape:
{"notEnoughData":false,"missingFactors":["string"],"differential":[{"diagnosis":"string","probability":0,"reasons":["string"]}],"redFlags":["string"],"suggestedWorkup":["string"]}
All human-readable text must be Persian.
""";
    }

    private static string FormatValue(StudyFactorValue value)
    {
        if (value.ValueNumber.HasValue) return value.ValueNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value.ValueBit.HasValue) return value.ValueBit.Value ? "yes" : "no";
        if (value.ValueDate.HasValue) return value.ValueDate.Value.ToString("yyyy-MM-dd");
        return value.ValueText ?? "-";
    }

    private int? GetCurrentUserID()
    {
        return int.TryParse(User.FindFirstValue("UserID"), out int id) ? id : null;
    }
}
