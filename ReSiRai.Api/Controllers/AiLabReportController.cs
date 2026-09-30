using System.Text.Json;
using ReSiRai.Api.Data;
using ReSiRai.Api.Models;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// Reads one photographed/printed laboratory report and returns a review-only
// draft of the tests, each one matched against the clinical factor dictionary.
// A report of several pages is sent as ONE AI call - the pages belong together
// and no result row is lost at a page break.
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

    public sealed class ExtractManyRequest
    {
        public int? StudyID { get; set; }
        public List<long> ImageIDs { get; set; } = new();
    }

    /// <summary>Extracts from one already attached image.</summary>
    [HttpPost("{imageID:long}/extract-lab")]
    public async Task<IActionResult> ExtractLab(long imageID, [FromQuery] int? studyID = null,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadImageAsync(imageID, cancellationToken);
        if (loaded.error is not null) return loaded.error;

        var image = loaded.image!;
        string path = _storage.GetPhysicalPath(image.RelativePath);
        byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        return await ExtractCoreAsync(new List<(string Mime, byte[] Bytes)> { (image.ContentType, bytes) },
            image.RelativePath, image.RelativePath, studyID, cancellationToken);
    }

    /// <summary>
    /// Extracts from several images of ONE report (a multi-page lab sheet). All
    /// pages are read together in a single AI call.
    /// </summary>
    [HttpPost("extract-lab")]
    public async Task<IActionResult> ExtractLabMany(ExtractManyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ImageIDs is null || request.ImageIDs.Count == 0)
            return BadRequest(new { success = false, message = "دست‌کم یک تصویر لازم است." });
        var ids = request.ImageIDs.Distinct().ToList();
        if (ids.Count > 10)
            return BadRequest(new { success = false, message = "حداکثر ۱۰ صفحه در هر استخراج قابل انتخاب است." });

        var parts = new List<(string Mime, byte[] Bytes)>();
        string firstRelative = "";
        string label = "";
        int page = 0;
        foreach (var id in ids)
        {
            var loaded = await LoadImageAsync(id, cancellationToken);
            if (loaded.error is not null) return loaded.error;

            var image = loaded.image!;
            string path = _storage.GetPhysicalPath(image.RelativePath);
            page++;
            if (page == 1) { firstRelative = image.RelativePath; label = image.RelativePath; }
            parts.Add((image.ContentType, await System.IO.File.ReadAllBytesAsync(path, cancellationToken)));
        }
        if (parts.Count > 1) label = $"{label} (+{parts.Count - 1} صفحه دیگر)";
        return await ExtractCoreAsync(parts, firstRelative, label, request.StudyID, cancellationToken);
    }

    /// <summary>
    /// Extracts from files picked on this device without attaching them to a
    /// visit first - the new-visit form needs the numbers before the visit row
    /// exists. Nothing is stored here; only the review draft is produced.
    /// </summary>
    [HttpPost("extract-lab-files")]
    public async Task<IActionResult> ExtractLabFiles(List<IFormFile> files,
        [FromQuery] int? studyID = null, CancellationToken cancellationToken = default)
    {
        if (files is null || files.Count == 0)
            return BadRequest(new { success = false, message = "دست‌کم یک فایل لازم است." });
        if (files.Count > 10)
            return BadRequest(new { success = false, message = "حداکثر ۱۰ صفحه در هر استخراج قابل انتخاب است." });

        var parts = new List<(string Mime, byte[] Bytes)>();
        string label = "";
        foreach (var f in files)
        {
            if (f.Length == 0) continue;
            if (string.IsNullOrWhiteSpace(f.ContentType)
                || !f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "برای استخراج، فایل‌ها باید تصویر (JPG/PNG) باشند." });
            await using var ms = new MemoryStream();
            await f.CopyToAsync(ms, cancellationToken);
            if (label.Length == 0) label = f.FileName;
            parts.Add((f.ContentType, ms.ToArray()));
        }
        if (parts.Count == 0)
            return BadRequest(new { success = false, message = "فایل تصویری معتبر انتخاب نشده است." });
        if (parts.Count > 1) label = $"{label} (+{parts.Count - 1} صفحه دیگر)";
        return await ExtractCoreAsync(parts, "", label, studyID, cancellationToken);
    }

    /// <summary>Loads one image and checks access, existence and file kind.</summary>
    private async Task<(RadiologyImage? image, IActionResult? error)> LoadImageAsync(long imageID,
        CancellationToken cancellationToken)
    {
        var image = await _db.RadiologyImages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ImageID == imageID, cancellationToken);
        if (image is null) return (null, NotFound(new { success = false, message = "تصویر پیدا نشد." }));

        var accessibleStudyIDs = _studyAccess.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User)
            .Select(x => x.StudyID);
        bool canAccess = StudyAccessService.IsSuperAdmin(User) || await _db.RadiologyStudyImages.AsNoTracking()
            .AnyAsync(x => x.ImageID == imageID && accessibleStudyIDs.Contains(x.StudyID), cancellationToken);
        if (!canAccess) return (null, NotFound(new { success = false, message = "تصویر پیدا نشد." }));

        if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return (null, BadRequest(new { success = false, message = "برای استخراج، تصویر باید JPG یا PNG باشد." }));

        string path = _storage.GetPhysicalPath(image.RelativePath);
        if (!System.IO.File.Exists(path))
            return (null, NotFound(new { success = false, message = "فایل تصویر روی دیسک پیدا نشد." }));

        return (image, null);
    }

    /// <summary>The shared extraction: one AI call over the pages, one batch row.</summary>
    private async Task<IActionResult> ExtractCoreAsync(List<(string Mime, byte[] Bytes)> parts,
        string firstRelativePath, string fileLabel, int? studyID, CancellationToken cancellationToken)
    {
        // The deterministic path first: a printed table is read from the OCR text
        // exactly as printed - no model, no network, and above all no invented
        // rows. The AI path below stays only for sheets this parser cannot read.
        var parsed = new List<LabSheetParser.Row>();
        var gate = new object();
        // Pages are read in parallel: a phone photo of a sheet takes seconds of
        // OCR each and doctors upload several at once.
        await Parallel.ForEachAsync(parts,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(parts.Count, 1, 3),
                CancellationToken = cancellationToken
            },
            async (part, ct) =>
            {
                try
                {
                    // مسیرِ قطعی: بازسازیِ جدول از TSV (مختصاتِ کلمات) که در
                    // برگه‌های متراکم ردیف‌ها را قاطی نمی‌کند؛ متنِ پشتیبان از
                    // همان TSV ساخته می‌شود، نه اجرای دوبارهٔ OCR.
                    string tsv = await _ai.OcrTsvAsync(part.Bytes, ct);
                    var rows = LabSheetParser.ParseTsv(tsv);
                    if (rows.Count == 0)
                        rows = LabSheetParser.Parse(LabSheetParser.TsvToText(tsv));
                    lock (gate) parsed.AddRange(rows);
                }
                catch (AiException) { /* no OCR on this machine - the AI path decides */ }
            });
        if (parsed.Count >= 3)
            return await BuildParsedResultAsync(parsed, firstRelativePath, fileLabel, studyID, cancellationToken);

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        string pagesIntro = parts.Count == 1
            ? "This image is a printed laboratory report, possibly in Persian and/or English."
            : $"These {parts.Count} images are the pages (in the given order) of ONE printed laboratory report, possibly in Persian and/or English. Read all pages together and extract every test result row across all of them.";
        string prompt = $$"""
{{pagesIntro}}
Transcribe only what is printed. Never guess values or invent rows.
Extract every test result row you can read.
Return ONLY valid JSON with this exact shape:
{"notALabReport":false,"labName":"string|null","sampleDate":"string|null","tests":[{"name":"string","nameFa":"string|null","value":"string","unit":"string|null","refText":"string|null","refLow":null,"refHigh":null,"flag":"string|null"}]}
- name: test name exactly as printed (Latin or Persian)
- nameFa: the standard Persian name of this test if you know it, otherwise null
- value: the result exactly as printed, number or text, without the unit
- unit: unit exactly as printed, e.g. mg/dL
- refText: reference interval exactly as printed
- refLow / refHigh: numeric bounds when the interval is numeric, otherwise null
- flag: H, L or normal when the report prints such a mark, otherwise null
- sampleDate: sampling date exactly as printed, keeping its calendar
- labName: laboratory name if printed
If the images are not laboratory reports, return {"notALabReport":true,"labName":null,"sampleDate":null,"tests":[]}
""";

        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(prompt, parts, cancellationToken);
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
                FileName = fileLabel,
                ImagePath = firstRelativePath.Length == 0 ? "" : _storage.GetPhysicalPath(firstRelativePath),
                LabName = GetString(root, "labName"),
                SampleDate = ParseDate(GetString(root, "sampleDate")),
                RawJson = raw,
                Status = 1,
                CreatedDate = DateTime.Now
            };
            _db.LabReportExtractions.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);

            var factors = await _db.ClinicalFactors.AsNoTracking()
                // A lab sheet only ever maps to lab factors - never to exam or
                // history observations that happen to share a word.
                .Where(x => x.IsActive && x.FactorCode.StartsWith("LAB."))
                .Select(x => new FactorInfo(x.FactorID, x.FactorCode, x.NameFa, x.NameEn, x.ShortCode, x.LoincCode, x.UnitUCUM, x.RefLow, x.RefHigh))
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
                        nameFa = GetString(t, "nameFa"),
                        value = GetString(t, "value"),
                        unit = GetString(t, "unit"),
                        refText = GetString(t, "refText"),
                        refLow = GetDecimal(t, "refLow"),
                        refHigh = GetDecimal(t, "refHigh"),
                        flag = GetString(t, "flag"),
                        factorID = best?.FactorID,
                        factorCode = best?.FactorCode,
                        factorNameFa = best?.NameFa,
                        factorShortCode = best?.ShortCode,
                        factorUnit = best?.UnitUCUM,
                        matchConfidence = best?.Confidence ?? 0,
                        matchStatus = best is null ? "unmatched" : (best.Confidence >= 95 ? "matched" : "review")
                    });
                }
            }

            return Ok(new
            {
                success = true,
                imageID = (long?)null,
                extractionID = batch.ExtractionID,
                labName = batch.LabName,
                sampleDateRaw = GetString(root, "sampleDate"),
                sampleDate = batch.SampleDate,
                unmatchedCount = unmatched,
                items
            });
        }
    }

    /// <summary>
    /// The parsed table becomes the review draft: the rows are exactly what the
    /// sheet printed (decimal point restored from the reference range), each one
    /// matched against the factor dictionary.
    /// </summary>
    private async Task<IActionResult> BuildParsedResultAsync(List<LabSheetParser.Row> rows,
        string firstRelativePath, string fileLabel, int? studyID, CancellationToken cancellationToken)
    {
        var batch = new LabReportExtraction
        {
            StudyID = studyID ?? 0,
            FileName = fileLabel,
            ImagePath = firstRelativePath.Length == 0 ? "" : _storage.GetPhysicalPath(firstRelativePath),
            LabName = null,
            SampleDate = null,
            RawJson = JsonSerializer.Serialize(rows),
            Status = 1,
            CreatedDate = DateTime.Now
        };
        _db.LabReportExtractions.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        var factors = await _db.ClinicalFactors.AsNoTracking()
            .Where(x => x.IsActive && x.FactorCode.StartsWith("LAB."))
            .Select(x => new FactorInfo(x.FactorID, x.FactorCode, x.NameFa, x.NameEn, x.ShortCode, x.LoincCode, x.UnitUCUM, x.RefLow, x.RefHigh))
            .ToListAsync(cancellationToken);

        var items = new List<object>();
        int unmatched = 0;
        foreach (var row in rows)
        {
            var best = MatchFactor(row.Name, factors);
            if (best is null) unmatched++;
            // The dictionary's own range is the better ruler for the scale fix.
            bool useDict = best?.RefLow is not null || best?.RefHigh is not null;
            string value = row.Value.Length == 0
                ? ""
                : LabSheetParser.FitScale(row.Value,
                    useDict ? best!.RefLow : row.RefLow, useDict ? best!.RefHigh : row.RefHigh);
            items.Add(new
            {
                name = row.Name,
                nameFa = (string?)null,
                value,
                unit = row.Unit,
                refText = row.RefText,
                refLow = row.RefLow,
                refHigh = row.RefHigh,
                flag = (string?)null,
                factorID = best?.FactorID,
                factorCode = best?.FactorCode,
                factorNameFa = best?.NameFa,
                factorShortCode = best?.ShortCode,
                factorUnit = best?.UnitUCUM,
                matchConfidence = best?.Confidence ?? 0,
                matchStatus = best is null ? "unmatched" : (best.Confidence >= 95 ? "matched" : "review")
            });
        }

        return Ok(new
        {
            success = true,
            imageID = (long?)null,
            extractionID = batch.ExtractionID,
            labName = (string?)null,
            sampleDateRaw = (string?)null,
            sampleDate = (DateTime?)null,
            unmatchedCount = unmatched,
            items
        });
    }

    private sealed record FactorInfo(int FactorID, string FactorCode, string NameFa, string NameEn, string? ShortCode, string? LoincCode, string? UnitUCUM, decimal? RefLow, decimal? RefHigh);
    private sealed record MatchResult(int FactorID, string FactorCode, string NameFa, string? ShortCode, string? UnitUCUM, int Confidence, decimal? RefLow, decimal? RefHigh);

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

            if (n == Norm(f.NameEn) || n == Norm(f.NameFa) || n == codeTail
                || (!string.IsNullOrEmpty(f.ShortCode) && n == Norm(f.ShortCode))) score = 100;
            else if (!string.IsNullOrEmpty(f.LoincCode) && n == Norm(f.LoincCode)) score = 100;
            else
            {
                // Containment only for near-length names: "NEU" inside
                // "Neurologic exam" is not a match - a lab sheet never maps to an
                // examination factor.
                string en = Norm(f.NameEn);
                int min = Math.Min(en.Length, n.Length);
                int max = Math.Max(en.Length, n.Length);
                score = min >= 5 && min * 2 >= max && (n.Contains(en) || en.Contains(n)) ? 80 : 0;
            }

            if (score >= 80 && (best is null || score > best.Confidence))
                best = new MatchResult(f.FactorID, f.FactorCode, f.NameFa, f.ShortCode, f.UnitUCUM, score, f.RefLow, f.RefHigh);
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
