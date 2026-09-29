using System.Text.Json;
using ReSiRai.Api.Data;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// The operator no longer picks an image type on upload: the AI looks at the
// image and chooses the type from the clinic's own list (OPG, CBCT, lab sheet,
// ID card, ...). The choice is advisory - the manual "set image type" button
// stays available whenever the AI could not decide.
[ApiController]
[Route("api/ai/images")]
public sealed class AiImageTypeController : ControllerBase
{
    private readonly ReSiRaiDbContext _db;
    private readonly RadiologyStorageService _storage;
    private readonly StudyAccessService _studyAccess;
    private readonly AiClient _ai;

    public AiImageTypeController(ReSiRaiDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, AiClient ai)
    {
        _db = db;
        _storage = storage;
        _studyAccess = studyAccess;
        _ai = ai;
    }

    [HttpPost("{imageID:long}/classify")]
    public async Task<IActionResult> Classify(long imageID, CancellationToken cancellationToken = default)
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
            return BadRequest(new { success = false, message = "تشخیص نوع فقط برای فایل تصویری انجام می‌شود." });

        string path = _storage.GetPhysicalPath(image.RelativePath);
        if (!System.IO.File.Exists(path))
            return NotFound(new { success = false, message = "فایل تصویر روی دیسک پیدا نشد." });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        var types = await _db.ImageTypes.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ImageTypeID)
            .Select(x => new { x.ImageTypeID, x.ImageTypeName })
            .ToListAsync(cancellationToken);
        if (types.Count == 0)
            return BadRequest(new { success = false, message = "هنوز نوع تصویری تعریف نشده است." });

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        string options = string.Join(", ", types.Select(t => $"{t.ImageTypeID}:{t.ImageTypeName}"));
        string prompt = $$"""
This image comes from a patient record. Decide what it is and choose the best
fitting image type ONLY from this list (id:name), comma separated:
{{options}}
Return ONLY valid JSON with this exact shape:
{"imageTypeID":number|null,"reason":"string"}
- imageTypeID: the id from the list that fits best; null when nothing fits at all
- reason: a very short phrase in Persian explaining the choice
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
            using var doc = JsonDocument.Parse(raw);
            int? picked = null;
            if (doc.RootElement.TryGetProperty("imageTypeID", out var idEl)
                && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out int id)
                && types.Any(t => t.ImageTypeID == id))
                picked = id;
            string? reason = doc.RootElement.TryGetProperty("reason", out var rEl)
                && rEl.ValueKind == JsonValueKind.String ? rEl.GetString() : null;

            if (picked.HasValue)
            {
                var tracked = await _db.RadiologyImages
                    .FirstOrDefaultAsync(x => x.ImageID == imageID, cancellationToken);
                if (tracked is not null && tracked.ImageTypeID != picked)
                {
                    tracked.ImageTypeID = picked;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            string? typeName = types.FirstOrDefault(t => t.ImageTypeID == picked)?.ImageTypeName;
            return Ok(new { success = true, imageID, imageTypeID = picked, imageTypeName = typeName, reason });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "پاسخ مدل قابل خواندن نبود." });
        }
    }
}
