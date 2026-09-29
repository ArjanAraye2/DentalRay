using System.Text.Json;
using System.Text.Json.Nodes;
using ReSiRai.Api.Data;
using ReSiRai.Api.Models;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// Analysis of one dental radiology image. The result is stored once per image, so the
// picture leaves the clinic only on the first analysis and later views are instant.
[ApiController]
[Route("api/ai/images")]
public sealed class AiRadiologyImageAnalysisController : ControllerBase
{
    private readonly ReSiRaiDbContext _db;
    private readonly RadiologyStorageService _storage;
    private readonly StudyAccessService _studyAccess;
    private readonly AiClient _ai;
    private readonly AppEventLogger _events;

    public AiRadiologyImageAnalysisController(ReSiRaiDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, AiClient ai, AppEventLogger events)
    {
        _db=db; _storage=storage; _studyAccess=studyAccess; _ai=ai; _events=events;
    }

    /// <summary>کاربرِ فعلی برای لاگ؛ نبودِ شناسه یعنی نامشخص.</summary>
    private int? CurrentUserID()
        => int.TryParse(User.FindFirst("UserID")?.Value, out int uid) && uid > 0 ? uid : (int?)null;

    /// <summary>نکتهٔ کاربر را به نتیجهٔ تحلیل می‌چسباند تا در نمایش و در تاریخچه بماند.</summary>
    private static string WithNote(string rawJson, string note)
    {
        if (note.Length == 0) return rawJson;
        var node = JsonNode.Parse(rawJson) as JsonObject;
        if (node is null) return rawJson;
        node["practitionerNote"] = note;
        return node.ToJsonString();
    }

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
        // نکتهٔ ویژهٔ کاربر همراهِ تصویر می‌رود («روی فلان قسمت بیشتر دقت کن»)؛
        // اگر نکته‌ای باشد، نتیجهٔ ذخیره‌شده کافی نیست و دوباره تحلیل می‌شود.
        string note = Request.Query["note"].ToString().Trim();
        if (note.Length > 500) note = note[..500];

        // یک تحلیلِ معتبر ذخیره شده ⇒ بدونِ ارسالِ دوبارهٔ تصویر همان بازمی‌گردد.
        bool force = string.Equals(Request.Query["force"], "1", StringComparison.OrdinalIgnoreCase) || note.Length > 0;
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
        if (note.Length > 0)
            prompt += "\n\nPractitioner's extra instruction (follow it, but still return ONLY the JSON described above):\n" + note;
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

        // نکتهٔ کاربر کنارِ نتیجه ذخیره می‌شود تا بعداً معلوم شود چه پرسیده شده.
        string resultJson = WithNote(raw, note);

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
            row.AnalysisJson = resultJson;
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
            Console.WriteLine($"[ReSiRai AI] save failed: {e.GetType().Name}: {e.Message}");
            row = null;
        }

        await _events.LogAsync("ai.image", detail:$"تحلیلِ تک‌تصویر — مدل {_ai.Model}", durationMs:(int)watch.ElapsedMilliseconds, userID:CurrentUserID());
        try{using var analysis=JsonDocument.Parse(resultJson);return Ok(new{success=true,imageID,analysis=analysis.RootElement.Clone(),note,cached=false,analyzedAt,model=_ai.Model,stale=false});}
        catch(JsonException){return StatusCode(502,new{success=false,message="فرمت نتیجه تحلیل معتبر نبود."});}
    }

    /// <summary>2 = چند تصویرِ مرتبط که با هم تحلیل شده‌اند (کشِ جدا از تک‌تصویر).</summary>
    private const byte KindManyImages = 2;
    /// <summary>پرامپتِ چندتصویری نسخهٔ مستقلِ خودش را دارد.</summary>
    private const int PromptVersionMany = 1;
    /// <summary>سقفِ تصویر در هر درخواستِ مشترک (با تحلیلِ همهٔ تصاویر یکی است).</summary>
    private const int MaxManyImages = 6;

    public sealed record AnalyzeManyRequest(long[]? ImageIDs, string? Note);

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

        // نکتهٔ ویژهٔ کاربر ⇒ تحلیلِ تازه لازم است.
        string note = (request?.Note ?? string.Empty).Trim();
        if (note.Length > 500) note = note[..500];

        bool force = string.Equals(Request.Query["force"], "1", StringComparison.OrdinalIgnoreCase) || note.Length > 0;
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
        if (note.Length > 0)
            prompt += "\n\nPractitioner's extra instruction (follow it, but still return ONLY the JSON described above):\n" + note;

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
                    ["note"] = note,
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
                Console.WriteLine($"[ReSiRai AI] multi-image save failed: {e.GetType().Name}: {e.Message}");
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
                note,
                cached = false,
                analyzedAt,
                model = _ai.Model,
                stale = false,
                truncated
            });
        }
    }

    // ---- گفت‌وگوی پیوسته پس از تحلیل ----------------------------------------
    // سرویسِ AI حافظه ندارد؛ پس زمینه را همین‌جا می‌سازیم: گزارشِ ذخیره‌شده +
    // پرسش‌های قبلی. تصویر فقط وقتی خودِ کاربر بخواهد دوباره می‌رود — یعنی
    // «دوباره آپلود» لازم نیست و پرسشِ متنی چند ثانیه‌ای جواب می‌دهد.

    public sealed record AskTurn(string? Question, string? Answer);
    public sealed record AskRequest(IReadOnlyList<long>? ImageIDs, string? Question, bool WithImages, IReadOnlyList<AskTurn>? History);

    /// <summary>گزارشِ ذخیره‌شدهٔ همین مجموعه تصویر؛ اگر ذخیره‌ای نباشد null.</summary>
    private async Task<string?> LoadReportAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 1)
        {
            var row = await _db.AIImageAnalyses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ImageID == ids[0] && x.Kind == KindRadiology, cancellationToken);
            return row?.AnalysisJson;
        }

        long key = ids.Min();
        var setRow = await _db.AIImageAnalyses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ImageID == key && x.Kind == KindManyImages, cancellationToken);
        if (setRow is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(setRow.AnalysisJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("imageIDs", out var stored) || stored.ValueKind != JsonValueKind.Array)
                return null;
            var storedIds = stored.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.Number ? e.GetInt64() : -1L)
                .OrderBy(x => x)
                .ToList();
            if (!storedIds.SequenceEqual(ids.OrderBy(x => x))) return null;
            return doc.RootElement.TryGetProperty("analysis", out var inner)
                ? inner.GetRawText()
                : doc.RootElement.GetRawText();
        }
        catch (JsonException) { return null; }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest? request, CancellationToken cancellationToken)
    {
        var ids = (request?.ImageIDs ?? Array.Empty<long>()).Distinct().Take(MaxManyImages).ToList();
        if (ids.Count == 0)
            return BadRequest(new { success = false, message = "تصویری انتخاب نشده است." });

        string question = (request?.Question ?? string.Empty).Trim();
        if (question.Length == 0)
            return BadRequest(new { success = false, message = "پرسش خالی است." });
        if (question.Length > 1000) question = question[..1000];

        bool withImages = request?.WithImages == true;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // دسترسی: همان قاعدهٔ تحلیل، برای تک‌تکِ تصویرها.
        if (!StudyAccessService.IsSuperAdmin(User))
        {
            var accessibleStudyIDs = _studyAccess.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User).Select(x => x.StudyID);
            var linked = await _db.RadiologyStudyImages.AsNoTracking()
                .Where(x => ids.Contains(x.ImageID) && accessibleStudyIDs.Contains(x.StudyID))
                .Select(x => x.ImageID)
                .Distinct()
                .ToListAsync(cancellationToken);
            var linkedSet = linked.ToHashSet();
            if (ids.Any(id => !linkedSet.Contains(id)))
                return NotFound(new { success = false, message = "تصویر پیدا نشد." });
        }

        string? report = await LoadReportAsync(ids, cancellationToken);
        if (report is null)
            return BadRequest(new { success = false, message = "ابتدا این تصویرها را تحلیل کنید؛ پرسش روی همان گزارش انجام می‌شود." });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        // ارسالِ دوبارهٔ تصویر فقط با همان تأییدِ نشست (consentِ تحلیل).
        if (withImages && !string.Equals(Request.Query["consent"], "1", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, needsConsent = true, message = "برای ارسالِ دوبارهٔ تصویر، تأییدِ حریم خصوصی لازم است." });

        var payload = new List<(string Mime, byte[] Bytes)>();
        if (withImages)
        {
            foreach (var id in ids)
            {
                var image = await _db.RadiologyImages.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ImageID == id, cancellationToken);
                if (image is null) continue;
                string path = _storage.GetPhysicalPath(image.RelativePath);
                if (!System.IO.File.Exists(path)) continue;
                payload.Add((image.ContentType, await System.IO.File.ReadAllBytesAsync(path, cancellationToken)));
            }
            if (payload.Count == 0)
                return NotFound(new { success = false, message = "فایل تصویری قابل خواندن پیدا نشد." });
        }

        // زمینه در همان یک پیام می‌رود چون سرویس بینِ درخواست‌ها حافظه ندارد.
        var context = new System.Text.StringBuilder();
        context.AppendLine("You are assisting a licensed practitioner with a follow-up question about an analysis report of their patient's images.");
        context.AppendLine("Answer in Persian. Base the answer on the report below and do not invent findings that are not in it.");
        context.AppendLine("When images are attached you may look at them again; otherwise, if the question needs another look, say plainly that the images must be sent again.");
        context.AppendLine("Never give a definitive diagnosis or a prescription. Keep the answer short and practical.");
        context.AppendLine();
        context.AppendLine("ANALYSIS REPORT (JSON):");
        context.AppendLine(report);

        var history = (request?.History ?? Array.Empty<AskTurn>())
            .Where(h => h is not null && !string.IsNullOrWhiteSpace(h.Question))
            .TakeLast(6)
            .ToList();
        if (history.Count > 0)
        {
            context.AppendLine();
            context.AppendLine("EARLIER EXCHANGE:");
            foreach (var turn in history)
            {
                context.AppendLine("Practitioner: " + turn.Question);
                if (!string.IsNullOrWhiteSpace(turn.Answer)) context.AppendLine("Assistant: " + turn.Answer);
            }
        }

        context.AppendLine();
        context.AppendLine("PRACTITIONER'S QUESTION:");
        context.AppendLine(question);
        context.AppendLine();
        context.AppendLine("Return ONLY valid JSON: {\"answer\":\"...\"} — every string in Persian.");

        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(context.ToString(), payload, cancellationToken, compactImages: true);
        }
        catch (AiException e)
        {
            await _events.LogAsync("ai.chat", outcome: "fail",
                detail: $"پرسش از AI ناموفق — {e.UserMessage}",
                durationMs: (int)watch.ElapsedMilliseconds, userID: CurrentUserID());
            return StatusCode(e.HttpStatus, new { success = false, message = e.UserMessage, detail = e.Detail });
        }

        string answer;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            answer = doc.RootElement.TryGetProperty("answer", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : doc.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "فرمت پاسخ AI معتبر نبود." });
        }
        if (answer.Trim().Length == 0)
            return StatusCode(502, new { success = false, message = "پاسخی دریافت نشد؛ دوباره تلاش کنید." });

        await _events.LogAsync("ai.chat",
            detail: $"پرسش از AI ({(withImages ? "با تصویر" : "بدونِ تصویر")}) — مدل {_ai.Model}",
            durationMs: (int)watch.ElapsedMilliseconds, userID: CurrentUserID());

        return Ok(new { success = true, answer, withImages, imageCount = ids.Count });
    }
}
