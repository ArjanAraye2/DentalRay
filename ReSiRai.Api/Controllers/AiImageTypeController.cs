using System.Text.Json;
using ReSiRai.Api.Data;
using ReSiRai.Api.Models;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// نوعِ تصویر را خودِ رسیرای تشخیص می‌دهد: نامِ فارسیِ کوتاه و آزاد، بدونِ نیاز
// به جدولِ انواع. فهرستِ واژگان در پرامپت فقط برایِ یکدستی است تا گروه‌های
// نمایشِ تصاویر شلوغ نشوند (مثلاً «سی تی» و «CT» و «سیتی‌اسکن» یکی شوند).
// تشخیص مشورتی است؛ دکمهٔ «تعیین نوع تصویر» برایِ اصلاحِ دستی می‌ماند.
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
            return BadRequest(new { success = false, message = "فقط تصویر قابل تشخیص نوع است؛ PDF نه." });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        var (picked, reason) = await ClassifyCoreAsync(image, cancellationToken);
        return Ok(new { success = true, imageID, imageTypeName = picked, reason });
    }

    // مراجعاتِ قدیمی: تصاویرِ بدونِ نوع، یک‌جا به تشخیصِ رسیرای سپرده می‌شوند.
    [HttpPost("classify-pending")]
    public async Task<IActionResult> ClassifyPending(CancellationToken cancellationToken = default)
    {
        var accessibleStudyIDs = _studyAccess.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User)
            .Select(x => x.StudyID);
        bool super = StudyAccessService.IsSuperAdmin(User);

        var pending = await (
                from i in _db.RadiologyImages.AsNoTracking()
                where i.ImageTypeText == null && i.ContentType.StartsWith("image/")
                where super || _db.RadiologyStudyImages.Any(l => l.ImageID == i.ImageID
                    && accessibleStudyIDs.Contains(l.StudyID))
                orderby i.ImageID
                select i)
            .Take(30)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return Ok(new { success = true, done = true, results = Array.Empty<object>() });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        var results = new List<object>();
        foreach (var image in pending)
        {
            var (picked, reason) = await ClassifyCoreAsync(image, cancellationToken);
            results.Add(new { imageID = image.ImageID, imageTypeName = picked, reason });
        }
        bool done = await _db.RadiologyImages.AsNoTracking()
            .AllAsync(x => x.ImageTypeText != null, cancellationToken);
        return Ok(new { success = true, done, results });
    }

    /// <summary>
    /// تشخیصِ نوعِ یک تصویر و ثبتِ آن. نام نرمال می‌شود تا مترادف‌ها در نمایش
    /// گروهِ جدا نسازند؛ هرگز چیزی ساخته نمی‌شود اگر مدل چیزی نگوید.
    /// </summary>
    private async Task<(string? Type, string? Reason)> ClassifyCoreAsync(
        RadiologyImage image, CancellationToken cancellationToken)
    {
        string path = _storage.GetPhysicalPath(image.RelativePath);
        if (!System.IO.File.Exists(path)) return (null, "فایل تصویر روی دیسک پیدا نشد.");

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        const string prompt = """
This image comes from a patient dental/medical record. Decide what it is and name its type.
Choose ONE of these canonical Persian type names when it fits (descriptions):
- «بایت‌وینگ»: dental bitewing X-ray (small film showing upper and lower back teeth side by side)
- «پری‌آپیکال»: single-tooth periapical X-ray
- «OPG»: panoramic dental X-ray (whole jaw in one wide film)
- «CBCT»: cone-beam CT slices montage or its report
- «سی‌تی‌اسکن»: medical CT images or CT report
- «سونوگرافی»: ultrasound image or report
- «MRI»: MRI image or report
- «PET»: PET image or report
- «اسکن هسته‌ای»: nuclear medicine / scintigraphy
- «رادیوگرافی»: plain medical X-ray (chest, limb, ...)
- «نسخه»: handwritten or printed prescription (drug list, recipe pad, treatment plan sketch)
- «برگهٔ آزمایش»: printed lab test sheet or table
- «فاکتور»: invoice or receipt
- «نامه یا گزارش پزشکی»: medical letter or typed report
- «کارت یا سند»: ID card, insurance card, official document
- «عکس بالینی»: intraoral / clinical photo of teeth or gums
- «سایر»: anything else — photos of objects, fabrics, rooms, unclear or non-medical pictures
Be honest: when the content does not CLEARLY fit a type, use «سایر». Never guess a
radiology type for handwriting, paper documents, or photos of objects.
Return ONLY valid JSON with this exact shape:
{"imageType":"string","confidence":number,"reason":"string"}
- confidence: 0..1 how sure you are; if below 0.6 you must use «سایر»
- reason: a very short phrase in Persian
""";

        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(prompt, new[] { (image.ContentType, bytes) }, cancellationToken);
        }
        catch (AiException e)
        {
            return (null, e.UserMessage);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            string? picked = doc.RootElement.TryGetProperty("imageType", out var tEl)
                && tEl.ValueKind == JsonValueKind.String ? tEl.GetString() : null;
            string? reason = doc.RootElement.TryGetProperty("reason", out var rEl)
                && rEl.ValueKind == JsonValueKind.String ? rEl.GetString() : null;
            // حدسِ کم‌اعتماد پذیرفته نمی‌شود: بهتر است «سایرِ» صادقانه باشد تا
            // «CBCT»ِ دروغین — گروه‌های نمایش باید قابلِ اعتماد بمانند.
            double confidence = doc.RootElement.TryGetProperty("confidence", out var cEl)
                && cEl.ValueKind == JsonValueKind.Number ? cEl.GetDouble() : 1.0;
            picked = CanonicalType(picked);
            if (confidence < 0.6) picked = "سایر";
            if (picked is null) return (null, reason);

            var tracked = await _db.RadiologyImages
                .FirstOrDefaultAsync(x => x.ImageID == image.ImageID, cancellationToken);
            if (tracked is not null && tracked.ImageTypeText != picked)
            {
                tracked.ImageTypeText = picked;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return (picked, reason);
        }
        catch (JsonException)
        {
            return (null, "پاسخِ هوش مصنوعی خوانده نشد.");
        }
    }

    /// <summary>
    /// یکدست‌سازیِ نامِ نوع: مترادف‌ها یکی می‌شوند و نامِ خالی حذف می‌شود تا
    /// گروه‌هایِ نمایش تمیز بمانند.
    /// </summary>
    private static string? CanonicalType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string t = raw.Trim();
        t = string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string key = t.Replace("\u200c", "").Replace(" ", "").ToLowerInvariant();
        return key switch
        {
            "" => null,
            "سایر" or "other" or "others" or "نامشخص" or "unknown" => "سایر",
            "سیتی" or "سیتیاسکن" or "ct" or "ctscan" or "cat" or "catscan" => "سی‌تی‌اسکن",
            "سونو" or "سونوگرافی" or "us" or "ultrasound" or "sonography" => "سونوگرافی",
            "امآرآی" or "mri" => "MRI",
            "pet" or "petscan" or "پت" or "پتهاست" => "PET",
            "اسکن" or "اسکنهستهای" or "nuclearscan" or "scintigraphy" or "سنتیگرافی" => "اسکن هسته‌ای",
            "رادیوگرافی" or "xray" or "x-ray" or "radiography" or "عکس" => "رادیوگرافی",
            "opg" or "پانورامیک" or "orthopantomogram" => "OPG",
            "cbct" => "CBCT",
            "برگهآزمایش" or "آزمایش" or "lab" or "labsheet" or "labreport" or "برگهآزمایشگاه" => "برگهٔ آزمایش",
            "نسخه" or "تجویز" or "prescription" or "rx" => "نسخه",
            "بایتوینگ" or "پایتونگی" or "پایتون‌وینگ" or "bitewing" or "bw" => "بایت‌وینگ",
            "پریاپیکال" or "periapical" => "پری‌آپیکال",
            "فاکتور" or "رسید" or "invoice" or "receipt" => "فاکتور",
            "نامه" or "گزارشپزشکی" or "نامهیاگزارشپزشکی" or "letter" or "report" => "نامه یا گزارش پزشکی",
            "کارت" or "سند" or "کارتیاسند" or "document" or "card" or "idcard" => "کارت یا سند",
            "عکسبالینی" or "بالینی" or "clinical" or "clinicalphoto" or "عکسدندان" => "عکس بالینی",
            _ => t
        };
    }
}
