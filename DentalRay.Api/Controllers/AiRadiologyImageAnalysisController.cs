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

    public AiRadiologyImageAnalysisController(DentalRayDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, AiClient ai)
    {
        _db=db; _storage=storage; _studyAccess=studyAccess; _ai=ai;
    }

    /// <summary>1 = radiology analysis (matches tblAIImageAnalyses.Kind).</summary>
    private const byte KindRadiology = 1;
    /// <summary>Bump when the prompt changes, so a stored result can be spotted as old.</summary>
    private const int PromptVersion = 1;

    [HttpPost("{imageID:long}/analyze-radiology")]
    public async Task<IActionResult> Analyze(long imageID, CancellationToken cancellationToken)
    {
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
                bool stale = !string.Equals(saved.Model, _ai.Model ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                return Ok(new
                {
                    success = true,
                    imageID,
                    analysis = cached.RootElement.Clone(),
                    cached = true,
                    analyzedAt = saved.AnalyzedAt,
                    model = saved.Model,
                    stale
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
You are assisting a licensed dentist by reviewing one dental radiology image.
Do not claim certainty, provide a definitive diagnosis, or prescribe treatment.
Identify only meaningful visible findings and use FDI tooth numbers when reasonably identifiable.
Separate visible findings, apparent previous dental work, and items suggested for dentist review.
Explicitly state uncertainty and never invent findings.
Return ONLY valid JSON with this shape:
{"generalFindings":"string","problemTeeth":[{"toothNumber":16,"findings":["..."],"previousWork":["..."],"dentistReview":["..."],"confidence":"low|medium|high"}]}
Write all explanatory strings in Persian.
""";
        string raw;
        try{raw=await _ai.CompleteJsonAsync(prompt,new[]{(image.ContentType,bytes)},cancellationToken);}
        catch(AiException e){return StatusCode(e.HttpStatus,new{success=false,message=e.UserMessage,detail=e.Detail});}

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
        catch { row = null; }

        try{using var analysis=JsonDocument.Parse(raw);return Ok(new{success=true,imageID,analysis=analysis.RootElement.Clone(),cached=false,analyzedAt,model=_ai.Model,stale=false});}
        catch(JsonException){return StatusCode(502,new{success=false,message="فرمت نتیجه تحلیل معتبر نبود."});}
    }
}
