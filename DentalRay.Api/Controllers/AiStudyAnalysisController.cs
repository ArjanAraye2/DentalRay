using System.Net.Http.Headers;
using System.Text;
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
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClients;

    public AiStudyAnalysisController(DentalRayDbContext db, RadiologyStorageService storage,
        IConfiguration configuration, IHttpClientFactory httpClients)
    {
        _db = db; _storage = storage; _configuration = configuration; _httpClients = httpClients;
    }

    [HttpPost("{studyID:int}/analyze")]
    public async Task<IActionResult> Analyze(int studyID, CancellationToken cancellationToken)
    {
        var study = await _db.RadiologyStudies.AsNoTracking().FirstOrDefaultAsync(x => x.StudyID == studyID, cancellationToken);
        if (study == null) return NotFound(new { success = false, message = "Study پیدا نشد." });

        var images = await (from link in _db.RadiologyStudyImages.AsNoTracking()
                            join image in _db.RadiologyImages.AsNoTracking() on link.ImageID equals image.ImageID
                            where link.StudyID == studyID
                            orderby image.FileName descending
                            select image).ToListAsync(cancellationToken);
        if (images.Count == 0) return BadRequest(new { success = false, message = "این Study تصویری برای تحلیل ندارد." });

        // PDF is intentionally excluded from image vision input for this first implementation.
        var supported = images.Where(x => x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)).ToList();
        if (supported.Count == 0) return BadRequest(new { success = false, message = "برای تحلیل AI حداقل یک تصویر JPG یا PNG لازم است." });

        string? apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(503, new { success = false, message = "OpenAI API Key برای DentalRay تنظیم نشده است." });
        string model = _configuration["OpenAI:Model"] ?? "gpt-5.6-sol";

        var content = new List<object>
        {
            new { type = "input_text", text = """
You are assisting a licensed dentist by reviewing all dental images belonging to one Study together.
Do not claim certainty or make a definitive diagnosis or treatment prescription from imaging alone.
Identify only teeth with meaningful visible findings. Use FDI tooth numbers when reasonably identifiable.
For each relevant tooth, separate: visible findings, apparent previous dental work, and items suggested for dentist review.
If a tooth number or finding is uncertain, state that uncertainty. Do not invent findings.
Return ONLY valid JSON with this shape:
{"generalFindings":"string","problemTeeth":[{"toothNumber":16,"findings":["..."],"previousWork":["..."],"dentistReview":["..."],"confidence":"low|medium|high"}]}
Write all explanatory strings in Persian.
""" }
        };

        foreach (var image in supported)
        {
            string path = _storage.GetPhysicalPath(image.RelativePath);
            if (!System.IO.File.Exists(path)) continue;
            byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
            string mime = image.ContentType.StartsWith("image/") ? image.ContentType : "image/jpeg";
            content.Add(new { type = "input_image", image_url = $"data:{mime};base64,{Convert.ToBase64String(bytes)}" });
        }
        if (content.Count == 1) return BadRequest(new { success = false, message = "فایل تصویری قابل خواندن پیدا نشد." });

        var requestBody = new
        {
            model,
            store = false,
            input = new[] { new { role = "user", content } },
            text = new { format = new { type = "json_object" } }
        };

        var client = _httpClients.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, cancellationToken);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return StatusCode(502, new { success = false, message = "سرویس AI پاسخ موفق نداد.", detail = raw.Length > 500 ? raw[..500] : raw });

        using var apiJson = JsonDocument.Parse(raw);
        string? outputText = null;
        if (apiJson.RootElement.TryGetProperty("output", out var output))
            foreach (var item in output.EnumerateArray())
                if (item.TryGetProperty("content", out var parts))
                    foreach (var part in parts.EnumerateArray())
                        if (part.TryGetProperty("type", out var t) && t.GetString() == "output_text" && part.TryGetProperty("text", out var txt))
                            outputText = txt.GetString();
        if (string.IsNullOrWhiteSpace(outputText))
            return StatusCode(502, new { success = false, message = "متن تحلیل از پاسخ AI دریافت نشد." });

        try
        {
            using var analysis = JsonDocument.Parse(outputText);
            return Ok(new { success = true, analysis = analysis.RootElement.Clone() });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "فرمت پاسخ AI معتبر نبود." });
        }
    }
}
