using System.Net.Http.Headers;
using System.Text;
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
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClients;

    public AiRadiologyImageAnalysisController(DentalRayDbContext db, RadiologyStorageService storage,
        StudyAccessService studyAccess, IConfiguration configuration, IHttpClientFactory httpClients)
    {
        _db=db; _storage=storage; _studyAccess=studyAccess; _configuration=configuration; _httpClients=httpClients;
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
        string? apiKey=_configuration["OpenAI:ApiKey"]??Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if(string.IsNullOrWhiteSpace(apiKey))return StatusCode(503,new{success=false,message="OpenAI API Key برای DentalRay تنظیم نشده است."});

        byte[] bytes=await System.IO.File.ReadAllBytesAsync(path,cancellationToken);
        string model=_configuration["OpenAI:Model"]??"gpt-5.6-sol";
        var content=new object[]
        {
            new {type="input_text",text="""
You are assisting a licensed dentist by reviewing one dental radiology image.
Do not claim certainty, provide a definitive diagnosis, or prescribe treatment.
Identify only meaningful visible findings and use FDI tooth numbers when reasonably identifiable.
Separate visible findings, apparent previous dental work, and items suggested for dentist review.
Explicitly state uncertainty and never invent findings.
Return ONLY valid JSON with this shape:
{"generalFindings":"string","problemTeeth":[{"toothNumber":16,"findings":["..."],"previousWork":["..."],"dentistReview":["..."],"confidence":"low|medium|high"}]}
Write all explanatory strings in Persian.
"""},
            new {type="input_image",image_url=$"data:{image.ContentType};base64,{Convert.ToBase64String(bytes)}"}
        };
        var requestBody=new{model,store=false,input=new[]{new{role="user",content}},text=new{format=new{type="json_object"}}};
        var client=_httpClients.CreateClient();
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",apiKey);
        request.Content=new StringContent(JsonSerializer.Serialize(requestBody),Encoding.UTF8,"application/json");
        using var response=await client.SendAsync(request,cancellationToken);
        string raw=await response.Content.ReadAsStringAsync(cancellationToken);
        if(!response.IsSuccessStatusCode)return StatusCode(502,new{success=false,message="سرویس تحلیل رادیولوژی پاسخ موفق نداد.",detail=raw.Length>500?raw[..500]:raw});

        using var apiJson=JsonDocument.Parse(raw);string? outputText=null;
        if(apiJson.RootElement.TryGetProperty("output",out var output))
            foreach(var item in output.EnumerateArray())if(item.TryGetProperty("content",out var parts))
                foreach(var part in parts.EnumerateArray())if(part.TryGetProperty("type",out var type)&&type.GetString()=="output_text"&&part.TryGetProperty("text",out var text))outputText=text.GetString();
        if(string.IsNullOrWhiteSpace(outputText))return StatusCode(502,new{success=false,message="نتیجه تحلیل از پاسخ سرویس دریافت نشد."});
        try{using var analysis=JsonDocument.Parse(outputText);return Ok(new{success=true,imageID,analysis=analysis.RootElement.Clone()});}
        catch(JsonException){return StatusCode(502,new{success=false,message="فرمت نتیجه تحلیل معتبر نبود."});}
    }
}
