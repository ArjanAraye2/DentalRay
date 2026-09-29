using System.Text.Json;
using ReSiRai.Api.Data;
using ReSiRai.Api.Models;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// Reads one photographed/printed laboratory report and returns a review-only
// draft of the tests, each one matched against the clinical factor dictionary.
//
// Nothing extracted by AI reaches the patient record automatically: the review
// screen shows the draft, a human confirms it, and the confirmed numbers are
// saved through POST api/factors/values with Source = 2 (lab-report extraction)
// and the ExtractionID of this batch - which keeps every number auditable.
[ApiController]
[Route("api/ai/images")]
public sealed class AiLabReportController : ControllerBase
{
    private readonly ReSiRaiDbContext _db;
    private readonly RadiologyStorageService _storage;
    private readonly StudyAccessService _studyAccess;
    private readonly AiClient _ai;

    public AiLabReportController(ReSiRaiDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, AiClient ai)
    {
        _db = db;
        _storage = storage;
        _studyAccess = studyAccess;
        _ai = ai;
    }

    [HttpPost("{imageID:long}/extract-lab")]
    public async Task<IActionResult> ExtractLab(long imageID, [FromQuery] int? studyID = null,
        CancellationToken cancellationToken = default)
    {
        var image = await _db.RadiologyImages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ImageID == imageID, cancellationToken);
        if (image is null) return NotFound(new { success = false, message = "تصویر پیدا نشد." });

        var accessibleStudyIDs = _studyAccess.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User)
            .Select(x => x.StudyID);
        bool canAccess = StudyAccessService.IsSuperAdmin(User) || await _db.RadiologyStudyImages.AsNoTracking()
            .AnyAsync(x => x.ImageID == imageID && accessibleStudyIDs.Contains(x.StudyID), cancellationToken);
        if (!canAccess) return NotFound(new { success = false, message = "تصویر پیدا نشد." });

        if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "برای استخراج، تصویر باید JPG یا PNG باشد." });

        string path = _storage.GetPhysicalPath(image.RelativePath);
        if (!System.IO.File.Exists(path))
            return NotFound(new { success = false, message = "فایل تصویر روی دیسک پیدا نشد." });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        string prompt = """
This image is a printed laboratory report, possibly in Persian and/or English.
Transcribe only what is printed. Never guess values or invent rows.
Extract every test result row you can read.
Return ONLY valid JSON with this exact shape:
{"notALabReport":false,"labName":"string|null","sampleDate":"string|null","tests":[{"name":"string","value":"string","unit":"string|null","refText":"string|null","refLow":null,"refHigh":null,"flag":"string|null"}]}
- name: test name exactly as printed (Latin or Persian)
- value: the result exactly as printed, number or text, without the unit
- unit: unit exactly as printed, e.g. mg/dL
- refText: reference interval exactly as printed
- refLow / refHigh: numeric bounds when the interval is numeric, otherwise null
- flag: H, L or normal when the report prints such a mark, otherwise null
- sampleDate: sampling date exactly as printed, keeping its calendar
- labName: laboratory name if printed
If the image is not a laboratory report, return {"notALabReport":true,"labName":null,"sampleDate":null,"tests":[]}
""";

        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(prompt, new[] { (image.ContentType, bytes) }, cancellationToken);
        }
        catch (AiException e)
        {
            return StatusCode(e.HttpStatus, new { success = false, message = e.UserMessage, detail = e.Detail });
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "پاسخ مدل قابل خواندن نبود." });
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("notALabReport", out var flagEl) && flagEl.ValueKind == JsonValueKind.True)
                return BadRequest(new { success = false, message = "این تصویر برگه آزمایش نیست." });

            // The batch row is the audit trail of this extraction: it is written even
            // if the review screen is abandoned, so nothing disappears silently.
            var batch = new LabReportExtraction
            {
                StudyID = studyID ?? 0,
                FileName = image.RelativePath,
                ImagePath = _storage.GetPhysicalPath(image.RelativePath),
                LabName = GetString(root, "labName"),
                SampleDate = ParseDate(GetString(root, "sampleDate")),
                RawJson = raw,
                Status = 1,
                CreatedDate = DateTime.Now
            };
            _db.LabReportExtractions.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);

            var factors = await _db.ClinicalFactors.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => new FactorInfo(x.FactorID, x.FactorCode, x.NameFa, x.NameEn, x.LoincCode, x.UnitUCUM))
                .ToListAsync(cancellationToken);

            var items = new List<object>();
            int unmatched = 0;
            if (root.TryGetProperty("tests", out var tests) && tests.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tests.EnumerateArray())
                {
                    string name = GetString(t, "name") ?? string.Empty;
                    var best = MatchFactor(name, factors);
                    if (best is null) unmatched++;

                    items.Add(new
                    {
                        name,
                        value = GetString(t, "value"),
                        unit = GetString(t, "unit"),
                        refText = GetString(t, "refText"),
                        refLow = GetDecimal(t, "refLow"),
                        refHigh = GetDecimal(t, "refHigh"),
                        flag = GetString(t, "flag"),
                        factorID = best?.FactorID,
                        factorCode = best?.FactorCode,
                        factorNameFa = best?.NameFa,
                        factorUnit = best?.UnitUCUM,
                        matchConfidence = best?.Confidence ?? 0,
                        matchStatus = best is null ? "unmatched" : (best.Confidence >= 95 ? "matched" : "review")
                    });
                }
            }

            return Ok(new
            {
                success = true,
                imageID,
                extractionID = batch.ExtractionID,
                labName = batch.LabName,
                sampleDateRaw = GetString(root, "sampleDate"),
                sampleDate = batch.SampleDate,
                unmatchedCount = unmatched,
                items
            });
        }
    }

    private sealed record FactorInfo(int FactorID, string FactorCode, string NameFa, string NameEn, string? LoincCode, string? UnitUCUM);
    private sealed record MatchResult(int FactorID, string FactorCode, string NameFa, string? UnitUCUM, int Confidence);

    /// <summary>
    /// Name matching between what the lab printed and our dictionary. Confidence
    /// drops from an exact name/alias hit to a partial one; below the threshold the
    /// row is returned unmatched so a human maps it by hand.
    /// </summary>
    private static MatchResult? MatchFactor(string printedName, List<FactorInfo> factors)
    {
        string n = Norm(printedName);
        if (n.Length == 0) return null;

        MatchResult? best = null;
        foreach (var f in factors)
        {
            int score;
            int dot = f.FactorCode.LastIndexOf('.');
            string codeTail = Norm(dot >= 0 ? f.FactorCode[(dot + 1)..] : f.FactorCode);

            if (n == Norm(f.NameEn) || n == Norm(f.NameFa) || n == codeTail) score = 100;
            else if (!string.IsNullOrEmpty(f.LoincCode) && n == Norm(f.LoincCode)) score = 100;
            else
            {
                string en = Norm(f.NameEn);
                score = en.Length >= 3 && (n.Contains(en) || en.Contains(n)) ? 80 : 0;
            }

            if (score >= 80 && (best is null || score > best.Confidence))
                best = new MatchResult(f.FactorID, f.FactorCode, f.NameFa, f.UnitUCUM, score);
        }
        return best;
    }

    /// <summary>Normalizes a printed name so spelling variants line up.</summary>
    private static string Norm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string s = raw.Trim().ToLowerInvariant()
            .Replace('ي', 'ی').Replace('ك', 'ک').Replace("\u200c", string.Empty);
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        string? s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static decimal? GetDecimal(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, out var dt) ? dt : null;
    }
}
