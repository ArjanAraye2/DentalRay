using System.Text.Json;
using DentalRay.Api.Data;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers;

// Runtime-only AI analysis. No AI result is written to DentalRay's database.
[ApiController]
[Route("api/ai/studies")]
public class AiStudyAnalysisController : ControllerBase
{
    private readonly DentalRayDbContext _db;
    private readonly RadiologyStorageService _storage;
    private readonly StudyAccessService _studyAccess;
    private readonly AiClient _ai;

    public AiStudyAnalysisController(DentalRayDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, AiClient ai)
    {
        _db = db;
        _storage = storage;
        _studyAccess = studyAccess;
        _ai = ai;
    }

    [HttpPost("{studyID:int}/analyze")]
    public async Task<IActionResult> Analyze(int studyID, CancellationToken cancellationToken)
    {
        // Authorization is checked before reading image metadata or physical files.
        if (!await _studyAccess.CanAccessStudyAsync(studyID, User))
            return NotFound(new { success = false, message = "مراجعه پیدا نشد." });

        var images = await (from link in _db.RadiologyStudyImages.AsNoTracking()
                            join image in _db.RadiologyImages.AsNoTracking() on link.ImageID equals image.ImageID
                            where link.StudyID == studyID
                            orderby image.FileName descending
                            select image).ToListAsync(cancellationToken);
        if (images.Count == 0) return BadRequest(new { success = false, message = "این مراجعه تصویری برای تحلیل ندارد." });

        // PDF is intentionally excluded from image vision input for this first implementation.
        var supported = images.Where(x => x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)).ToList();
        if (supported.Count == 0) return BadRequest(new { success = false, message = "برای تحلیل AI حداقل یک تصویر JPG یا PNG لازم است." });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        // حداکثرِ ۶ تصویر در یک درخواست: هم سقفِ سرویس را نمی‌زند و هم ارسالِ
        // همزمانِ ده‌ها تصویر خط را می‌بُرد. اگر بیشتر باشد، این موضوع به
        // رابطِ کاربری گفته می‌شود تا پزشک بداند نتیجه ناقص است.
        const int maxImages = 6;
        var selected = supported.Take(maxImages).ToList();
        bool truncated = supported.Count > selected.Count;

        var payload = new List<(string Mime, byte[] Bytes)>();
        foreach (var image in selected)
        {
            string path = _storage.GetPhysicalPath(image.RelativePath);
            if (!System.IO.File.Exists(path)) continue;
            byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
            payload.Add((image.ContentType, bytes));
        }
        if (payload.Count == 0)
            return BadRequest(new { success = false, message = "فایل تصویری قابل خواندن پیدا نشد." });

        string prompt = """
You are assisting a licensed dentist by reviewing all dental images belonging to one Study together.
Do not claim certainty or make a definitive diagnosis or treatment prescription from imaging alone.
Identify only teeth with meaningful visible findings. Use FDI tooth numbers when reasonably identifiable.
For each relevant tooth, separate: visible findings, apparent previous dental work, and items suggested for dentist review.
If a tooth number or finding is uncertain, state that uncertainty. Do not invent findings.
Return ONLY valid JSON with this shape:
{"generalFindings":"string","problemTeeth":[{"toothNumber":16,"findings":["..."],"previousWork":["..."],"dentistReview":["..."],"confidence":"low|medium|high"}]}
Write all explanatory strings in Persian.
""";

        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(prompt, payload, cancellationToken, compactImages: true);
        }
        catch (AiException e)
        {
            return StatusCode(e.HttpStatus, new { success = false, message = e.UserMessage, detail = e.Detail });
        }

        try
        {
            using var analysis = JsonDocument.Parse(raw);
            return Ok(new { success = true, analysis = analysis.RootElement.Clone(), truncated });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "فرمت پاسخ AI معتبر نبود." });
        }
    }
}
