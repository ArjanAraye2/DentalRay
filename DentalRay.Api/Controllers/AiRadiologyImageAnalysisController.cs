using System.Text.Json;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers;

// Analysis of one dental radiology image. The result is stored once per image, so the
// picture leaves the clinic only on the first analysis and later views are instant.
[ApiController]
[Route("api/ai/images")]
public sealed class AiRadiologyImageAnalysisController : ControllerBase
{
    private readonly DentalRayDbContext _db;
    private readonly RadiologyStorageService _storage;
    private readonly StudyAccessService _studyAccess;
    private readonly AiClient _ai;
    private readonly AppEventLogger _events;

    public AiRadiologyImageAnalysisController(DentalRayDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, AiClient ai, AppEventLogger events)
    {
        _db=db; _storage=storage; _studyAccess=studyAccess; _ai=ai; _events=events;
    }

    /// <summary>کاربرِ فعلی برای لاگ؛ نبودِ شناسه یعنی نامشخص.</summary>
    private int? CurrentUserID()
        => int.TryParse(User.FindFirst("UserID")?.Value, out int uid) && uid > 0 ? uid : (int?)null;

    /// <summary>1 = radiology analysis (matches tblAIImageAnalyses.Kind).</summary>
    private const byte KindRadiology = 1;
    /// <summary>Bump when the prompt changes, so a stored result can be spotted as old.</summary>
    private const int PromptVersion = 4;

    [HttpPost("{imageID:long}/analyze-radiology")]
    public async Task<IActionResult> Analyze(long imageID, CancellationToken cancellationToken)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var image=await _db.RadiologyImages.AsNoTracking().FirstOrDefaultAsync(x=>x.ImageID==imageID,cancellationToken);
        if(image is null)return NotFound(new{success=false,message="تصویر پیدا نشد."});
        var accessibleStudyIDs=_studyAccess.ApplyAccess(_db.RadiologyStudies.AsNoTracking(),User).Select(x=>x.StudyID);
        bool canAccess=StudyAccessService.IsSuperAdmin(User)||await _db.RadiologyStudyImages.AsNoTracking()
            .AnyAsync(x=>x.ImageID==imageID&&accessibleStudyIDs.Contains(x.StudyID),cancellationToken);
        if(!canAccess)return NotFound(new{success=false,message="تصویر پیدا نشد."});
        if(!image.ContentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase))
            return BadRequest(new{success=false,message="تحلیل فقط برای فایل تصویری انجام می‌شود."});

        string path=_storage.GetPhysicalPath(image.RelativePath);
        if(!System.IO.File.Exists(path))return NotFound(new{success=false,message="فایل فیزیکی تصویر پیدا نشد."});
        // یک تحلیلِ معتبر ذخیره شده ⇒ بدونِ ارسالِ دوبارهٔ تصویر همان بازمی‌گردد.
        bool force = string.Equals(Request.Query["force"], "1", StringComparison.OrdinalIgnoreCase);
        AIImageAnalysis? saved = await _db.AIImageAnalyses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ImageID == imageID && x.Kind == KindRadiology, cancellationToken);
        if (saved is not null && !force)
        {
            try
            {
                using var cached = JsonDocument.Parse(saved.AnalysisJson);
                bool modelChanged = !string.Equals(saved.Model, _ai.Model ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                bool promptChanged = saved.PromptVersion < PromptVersion;
                bool stale = modelChanged || promptChanged;
                string? staleReason = promptChanged ? "prompt" : modelChanged ? "model" : null;
                return Ok(new
                {
                    success = true,
                    imageID,
                    analysis = cached.RootElement.Clone(),
                    cached = true,
                    analyzedAt = saved.AnalyzedAt,
                    model = saved.Model,
                    stale,
                    staleReason
                });
            }
            catch (JsonException) { saved = null; } // نتیجهٔ خراب ⇒ مثلِ اینکه ذخیره‌ای نبود
        }

        string? configError=_ai.ConfigurationError();
        if(configError is not null)return StatusCode(503,new{success=false,message=configError});

        // بدونِ تأییدِ صریحِ کاربر تصویر ارسال نمی‌شود — اینجا خودِ سرور هم اجرا می‌کند.
        bool consent = string.Equals(Request.Query["consent"], "1", StringComparison.OrdinalIgnoreCase);
        if (!consent)
            return BadRequest(new
            {
                success = false,
                needsConsent = true,
                message = "برای ارسالِ تصویر به سرویسِ خارجی، تأییدِ حریم خصوصی لازم است."
            });

        byte[] bytes=await System.IO.File.ReadAllBytesAsync(path,cancellationToken);
        string prompt="""
You are a clinical assistant reviewing one medical image for a licensed practitioner.
The image may be a radiograph, a CT or MRI slice, an ultrasound, a clinical photograph,
or a non-medical picture; the clinic is not necessarily dental.

Work through the image systematically and report in detail:
1. modality: what kind of image it is. anatomy: the body part or region in view.
2. generalFindings: a compact overview of the whole study in two or three sentences.
3. findings: an itemised region-by-region review. For EVERY region visible in the image
   give: region (organ or area), observation (what is actually seen there - write
   "نرمال" when it looks normal), suggestion (what deserves a closer look and why),
   and confidence (low|medium|high). Do not skip visible organs; be specific about
   size, shape, density or position whenever the image allows it.
4. When teeth are visible, also fill problemTeeth with FDI numbers; otherwise leave it empty.
Never claim certainty, never give a definitive diagnosis, never prescribe treatment,
and never invent findings. If the image is not medical, say plainly what it is and
return an empty findings array.
Return ONLY valid JSON with this shape:
{"modality":"string","anatomy":"string","generalFindings":"string","findings":[{"region":"string","observation":"string","suggestion":"string","confidence":"low|medium|high"}],"problemTeeth":[{"toothNumber":16,"findings":["..."],"previousWork":["..."],"dentistReview":["..."],"confidence":"low|medium|high"}]}
EVERY string must be written in Persian (Farsi) - never answer in English.
""";
        string raw;
        try{raw=await _ai.CompleteJsonAsync(prompt,new[]{(image.ContentType,bytes)},cancellationToken);}
        catch(AiException e)
        {
            // فقط تلاشِ واقعی در لاگ می‌ماند؛ پاسخِ ذخیره‌شده رویدادی نیست.
            await _events.LogAsync("ai.image", outcome:"fail",
                detail:$"تحلیلِ تک‌تصویر ناموفق — {e.UserMessage}",
                durationMs:(int)watch.ElapsedMilliseconds, userID:CurrentUserID());
            return StatusCode(e.HttpStatus,new{success=false,message=e.UserMessage,detail=e.Detail});
        }

        // ذخیره تا تصویر فقط یک بار از مطب خارج شود؛ اگر ذخیره هم نشد، تحلیل از بین نرود.
        AIImageAnalysis? row = saved;
        DateTime? analyzedAt = null;
        try
        {
            if (row is null)
            {
                row = new AIImageAnalysis { ImageID = imageID, Kind = KindRadiology };
                _db.AIImageAnalyses.Add(row);
            }
            row.AnalysisJson = raw;
            row.Model = _ai.Model ?? string.Empty;
            row.PromptVersion = PromptVersion;
            row.AnalyzedAt = DateTime.Now;
            row.AnalyzedByUserID = int.TryParse(User.FindFirst("UserID")?.Value, out int uid) && uid > 0 ? uid : (int?)null;
            await _db.SaveChangesAsync(cancellationToken);
            analyzedAt = row.AnalyzedAt;
        }
        catch (Exception e)
        {
            // ذخیره نباید نتیجهٔ تحلیل را از بین ببرد، ولی علتِ شکست باید در لاگ
            // بماند تا قابلِ پیگیری باشد.
            Console.WriteLine($"[DentalRay AI] save failed: {e.GetType().Name}: {e.Message}");
            row = null;
        }

        await _events.LogAsync("ai.image", detail:$"تحلیلِ تک‌تصویر — مدل {_ai.Model}", durationMs:(int)watch.ElapsedMilliseconds, userID:CurrentUserID());
        try{using var analysis=JsonDocument.Parse(raw);return Ok(new{success=true,imageID,analysis=analysis.RootElement.Clone(),cached=false,analyzedAt,model=_ai.Model,stale=false});}
        catch(JsonException){return StatusCode(502,new{success=false,message="فرمت نتیجه تحلیل معتبر نبود."});}
    }

    /// <summary>2 = چند تصویرِ مرتبط که با هم تحلیل شده‌اند (کشِ جدا از تک‌تصویر).</summary>
    private const byte KindManyImages = 2;
    /// <summary>پرامپتِ چندتصویری نسخهٔ مستقلِ خودش را دارد.</summary>
    private const int PromptVersionMany = 1;
    /// <summary>سقفِ تصویر در هر درخواستِ مشترک (با تحلیلِ همهٔ تصاویر یکی است).</summary>
    private const int MaxManyImages = 6;

    public sealed record AnalyzeManyRequest(long[]? ImageIDs);

    // چند تصویرِ انتخاب‌شده که به هم مرتبط‌اند یک‌جا بررسی می‌شوند. نتیجه برای همان
    // مجموعه ذخیره می‌شود؛ بنابراین اگر همان تصویرها را دوباره انتخاب کنید، بدونِ
    // ارسالِ دوباره همان نتیجه برمی‌گردد.
    [HttpPost("analyze-many")]
    public async Task<IActionResult> AnalyzeMany([FromBody] AnalyzeManyRequest? request, CancellationToken cancellationToken)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var requested = (request?.ImageIDs ?? Array.Empty<long>()).Distinct().ToList();
        if (requested.Count < 2)
            return BadRequest(new { success = false, message = "برای تحلیلِ مشترک، حداقل ۲ تصویر انتخاب کنید." });
        if (requested.Count > 64)
            return BadRequest(new { success = false, message = "تعداد تصویرهای انتخاب‌شده بیش از حد مجاز است." });

        var images = await _db.RadiologyImages.AsNoTracking()
            .Where(x => requested.Contains(x.ImageID))
            .ToListAsync(cancellationToken);
        if (images.Count != requested.Count)
            return NotFound(new { success = false, message = "یکی از تصویرهای انتخاب‌شده پیدا نشد." });

        // ترتیبِ ارسالِ کاربر (در گرید از جدیدترین) حفظ می‌شود.
        var order = requested.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        images = images.OrderBy(x => order[x.ImageID]).ToList();

        // دسترسی باید برای تک‌تکِ تصویرها اثبات شود، نه فقط برای یکی‌شان.
        if (!StudyAccessService.IsSuperAdmin(User))
        {
            var accessibleStudyIDs = _studyAccess.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User).Select(x => x.StudyID);
            var linked = await _db.RadiologyStudyImages.AsNoTracking()
                .Where(x => requested.Contains(x.ImageID) && accessibleStudyIDs.Contains(x.StudyID))
                .Select(x => x.ImageID)
                .Distinct()
                .ToListAsync(cancellationToken);
            var linkedSet = linked.ToHashSet();
            if (requested.Any(id => !linkedSet.Contains(id)))
                return NotFound(new { success = false, message = "تصویر پیدا نشد." });
        }

        if (images.Any(x => !x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { success = false, message = "در تحلیلِ مشترک فقط تصویر (JPG/PNG) انتخاب کنید؛ PDF این‌جا قابل ارسال نیست." });

        var selected = images.Take(MaxManyImages).ToList();
        bool truncated = images.Count > selected.Count;

        // کلیدِ کش: مجموعهٔ مرتبِ همین تصویرها روی کوچک‌ترین شماره‌شان ذخیره می‌شود
        // و فقط وقتی برگردانده می‌شود که دقیقاً همان مجموعه انتخاب شده باشد.
        var setIds = selected.Select(x => x.ImageID).OrderBy(x => x).ToList();
        long keyImageID = setIds[0];

        bool force = string.Equals(Request.Query["force"], "1", StringComparison.OrdinalIgnoreCase);
        AIImageAnalysis? saved = await _db.AIImageAnalyses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ImageID == keyImageID && x.Kind == KindManyImages, cancellationToken);
        if (saved is not null && !force)
        {
            try
            {
                using var cached = JsonDocument.Parse(saved.AnalysisJson);
                bool sameSet = false;
                if (cached.RootElement.ValueKind == JsonValueKind.Object
                    && cached.RootElement.TryGetProperty("imageIDs", out var cachedIds)
                    && cachedIds.ValueKind == JsonValueKind.Array)
                {
                    sameSet = cachedIds.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.Number ? e.GetInt64() : -1L)
                        .OrderBy(x => x)
                        .SequenceEqual(setIds);
                }
                if (sameSet && cached.RootElement.TryGetProperty("analysis", out var cachedAnalysis))
                {
                    bool modelChanged = !string.Equals(saved.Model, _ai.Model ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    bool promptChanged = saved.PromptVersion < PromptVersionMany;
                    bool stale = modelChanged || promptChanged;
                    string? staleReason = promptChanged ? "prompt" : modelChanged ? "model" : null;
                    return Ok(new
                    {
                        success = true,
                        imageIDs = setIds,
                        imageCount = selected.Count,
                        analysis = cachedAnalysis.Clone(),
                        cached = true,
                        analyzedAt = saved.AnalyzedAt,
                        model = saved.Model,
                        stale,
                        staleReason,
                        truncated
                    });
                }
            }
            // ذخیرهٔ خراب یا مجموعهٔ دیگر ⇒ مثلِ اینکه چیزی نبود و دوباره تحلیل می‌شود
            // (و همان سطرِ قبلی با نتیجهٔ تازه جایگزین می‌شود).
            catch (JsonException) { }
        }

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        // تأییدِ حریم خصوصی روی سرور اجرا می‌شود: بدونِ consent هیچ فایلی خوانده نمی‌شود.
        bool consent = string.Equals(Request.Query["consent"], "1", StringComparison.OrdinalIgnoreCase);
        if (!consent)
            return BadRequest(new
            {
                success = false,
                needsConsent = true,
                message = "برای ارسالِ تصویرها به سرویسِ خارجی، تأییدِ حریم خصوصی لازم است."
            });

        var payload = new List<(string Mime, byte[] Bytes)>();
        foreach (var image in selected)
        {
            string path = _storage.GetPhysicalPath(image.RelativePath);
            if (!System.IO.File.Exists(path))
                return NotFound(new { success = false, message = "فایل فیزیکی یکی از تصویرهای انتخاب‌شده پیدا نشد." });
            byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
            payload.Add((image.ContentType, bytes));
        }

        string prompt = """
You are a clinical assistant reviewing a set of related medical images that belong to one
case, handed to you together so findings can be connected across them.
The images may be radiographs, CT or MRI slices, ultrasounds, clinical photographs,
or non-medical pictures; the clinic is not necessarily dental.

Work through the whole set systematically and report in detail:
1. modality: what kind of image it is (name it per image when they differ).
   anatomy: the body part or region in view.
2. generalFindings: a compact overview of the whole set in two or three sentences,
   connecting what the images show together.
3. findings: an itemised region-by-region review across ALL images. For EVERY region
   visible give: region (organ or area), observation (what is actually seen there - write
   "نرمال" when it looks normal), suggestion (what deserves a closer look and why),
   and confidence (low|medium|high). Do not skip visible organs; be specific about
   size, shape, density or position whenever the images allow it. When the same region
   appears in more than one image, say how the views agree or differ.
4. When teeth are visible, also fill problemTeeth with FDI numbers; otherwise leave it empty.
Never claim certainty, never give a definitive diagnosis, never prescribe treatment,
and never invent findings. If the images are not medical, say plainly what they are
and return an empty findings array.
Return ONLY valid JSON with this shape:
{"modality":"string","anatomy":"string","generalFindings":"string","findings":[{"region":"string","observation":"string","suggestion":"string","confidence":"low|medium|high"}],"problemTeeth":[{"toothNumber":16,"findings":["..."],"previousWork":["..."],"dentistReview":["..."],"confidence":"low|medium|high"}]}
EVERY string must be written in Persian (Farsi) - never answer in English.
""";

        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(prompt, payload, cancellationToken, compactImages: true);
        }
        catch (AiException e)
        {
            await _events.LogAsync("ai.images", outcome: "fail",
                detail: $"تحلیلِ {selected.Count} تصویر ناموفق — {e.UserMessage}",
                durationMs: (int)watch.ElapsedMilliseconds, userID: CurrentUserID());
            return StatusCode(e.HttpStatus, new { success = false, message = e.UserMessage, detail = e.Detail });
        }

        JsonDocument analysis;
        try
        {
            analysis = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "فرمت نتیجه تحلیل معتبر نبود." });
        }

        using (analysis)
        {
            // ذخیره تا همان مجموعه بدونِ ارسالِ دوباره قابلِ بازخوانی باشد؛ اگر ذخیره
            // شکست خورد، خودِ نتیجه نباید از بین برود.
            DateTime? analyzedAt = null;
            try
            {
                AIImageAnalysis row = saved ?? new AIImageAnalysis { ImageID = keyImageID, Kind = KindManyImages };
                if (saved is null) _db.AIImageAnalyses.Add(row);
                row.AnalysisJson = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["imageIDs"] = setIds,
                    ["analysis"] = analysis.RootElement.Clone()
                });
                row.Model = _ai.Model ?? string.Empty;
                row.PromptVersion = PromptVersionMany;
                row.AnalyzedAt = DateTime.Now;
                row.AnalyzedByUserID = int.TryParse(User.FindFirst("UserID")?.Value, out int uid) && uid > 0 ? uid : (int?)null;
                await _db.SaveChangesAsync(cancellationToken);
                analyzedAt = row.AnalyzedAt;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[DentalRay AI] multi-image save failed: {e.GetType().Name}: {e.Message}");
            }

            await _events.LogAsync("ai.images",
                detail: $"تحلیلِ {selected.Count} تصویر با هم — مدل {_ai.Model}{(truncated ? " (بیش از ۶ تصویر: فقط ۶ تای اول)" : "")}",
                durationMs: (int)watch.ElapsedMilliseconds, userID: CurrentUserID());

            return Ok(new
            {
                success = true,
                imageIDs = setIds,
                imageCount = selected.Count,
                analysis = analysis.RootElement.Clone(),
                cached = false,
                analyzedAt,
                model = _ai.Model,
                stale = false,
                truncated
            });
        }
    }
}
