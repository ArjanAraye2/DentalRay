using System.Text.Json;
using DentalRay.Api.Data;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers;

// Reads one photographed legacy patient card and returns a review-only draft.
// Nothing extracted by AI is written to the database automatically.
[ApiController]
[Route("api/ai/images")]
public sealed class AiCardExtractionController : ControllerBase
{
    private readonly DentalRayDbContext _db;
    private readonly RadiologyStorageService _storage;
    private readonly StudyAccessService _studyAccess;
    private readonly AiClient _ai;

    public AiCardExtractionController(DentalRayDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, AiClient ai)
    {
        _db = db;
        _storage = storage;
        _studyAccess = studyAccess;
        _ai = ai;
    }

    [HttpPost("{imageID:long}/extract-card")]
    public async Task<IActionResult> ExtractCard(long imageID, CancellationToken cancellationToken)
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
            return BadRequest(new { success = false, message = "استخراج نوشته فقط از فایل تصویری JPG یا PNG انجام می‌شود." });

        string path = _storage.GetPhysicalPath(image.RelativePath);
        if (!System.IO.File.Exists(path))
            return NotFound(new { success = false, message = "فایل فیزیکی تصویر پیدا نشد." });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        string prompt = """
This image is a photographed legacy dental patient card, possibly handwritten in Persian.
Transcribe only what is visible. Never guess missing characters or values.
Extract only candidate Study fields. Patient identity and contact fields are already known and must be ignored.
Use null for absent or unreadable values.
Keep dates exactly as written and do not convert calendars. Put uncertain field names in uncertainFields.
Return ONLY valid JSON with this exact shape:
{"rawText":"string","study":{"studyDate":null,"studyType":null,"bodyPart":null,"description":null,"report":null},"uncertainFields":[]}
All descriptive text must be Persian.
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

        try
        {
            using var extraction = JsonDocument.Parse(raw);
            return Ok(new { success = true, imageID, extraction = extraction.RootElement.Clone() });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "فرمت اطلاعات استخراج‌شده معتبر نبود." });
        }
    }
}
