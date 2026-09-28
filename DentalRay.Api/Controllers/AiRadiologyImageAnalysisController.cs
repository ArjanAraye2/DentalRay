using System.Text.Json;
using DentalRay.Api.Data;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers;

// Runtime-only analysis of one dental radiology image. The result is not persisted.
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
        string? configError=_ai.ConfigurationError();
        if(configError is not null)return StatusCode(503,new{success=false,message=configError});

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

        try{using var analysis=JsonDocument.Parse(raw);return Ok(new{success=true,imageID,analysis=analysis.RootElement.Clone()});}
        catch(JsonException){return StatusCode(502,new{success=false,message="فرمت نتیجه تحلیل معتبر نبود."});}
    }
}
