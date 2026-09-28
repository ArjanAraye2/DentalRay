using System.Text.Json;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DentalRay.Api.Controllers;

// Reads a photographed legacy patient card for the "new visit" form.
//
// The picture is not stored here: the browser keeps it and attaches it to the visit
// only after the visit itself exists, so a cancelled form never leaves an orphan
// document in the patient record.
[ApiController]
[Route("api/ai")]
public sealed class AiStudyCardController : ControllerBase
{
    private const int MaxBytes = 10 * 1024 * 1024;
    private readonly AiClient _ai;

    public AiStudyCardController(AiClient ai)
    {
        _ai = ai;
    }

    [HttpPost("study-card")]
    public async Task<IActionResult> Extract([FromForm] IFormFile? card, CancellationToken cancellationToken)
    {
        if (card is null || card.Length == 0)
            return BadRequest(new { success = false, message = "عکس یا اسکنی ارسال نشده است." });
        if (card.Length > MaxBytes)
            return BadRequest(new { success = false, message = "حجمِ تصویر بیش از ۱۰ مگابایت است." });
        if (!(card.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
            return BadRequest(new { success = false, message = "فایلِ ارسالی باید تصویر (JPG یا PNG) باشد." });

        // سرور هم تأیید را اجرا می‌کند: بدونِ consent هیچ تصویری ارسال نمی‌شود.
        bool consent = string.Equals(Request.Query["consent"], "1", StringComparison.OrdinalIgnoreCase);
        if (!consent)
            return BadRequest(new
            {
                success = false,
                needsConsent = true,
                message = "برای ارسالِ عکسِ کارت به سرویسِ خارجی، تأییدِ حریم خصوصی لازم است."
            });

        string? configError = _ai.ConfigurationError();
        if (configError is not null)
            return StatusCode(503, new { success = false, message = configError });

        byte[] bytes;
        await using (var stream = new MemoryStream())
        {
            await card.CopyToAsync(stream, cancellationToken);
            bytes = stream.ToArray();
        }

        string prompt = """
This image is a photographed legacy dental patient record card, possibly handwritten in Persian.
Transcribe only what is visible. Never guess missing characters or values, and never invent findings.
Extract everything needed to register a new visit: the date, the reason for visit, the body part,
the description, the report, tooth numbers when they are written, and any other note such as
previous treatment, medication or referral.
Use null for absent or unreadable values. Keep the date exactly as written; ALSO give
studyDateNormalized as the Jalali date in YYYY/MM/DD or YYYY/MM/DD HH:mm when it can be read reliably.
Return ONLY valid JSON with this exact shape:
{"rawText":"string","study":{"studyDate":null,"studyDateNormalized":null,"studyType":null,"bodyPart":null,"description":null,"report":null,"teeth":[]},"otherNotes":[],"uncertainFields":[]}
teeth must be an array of FDI tooth numbers (for example 16 or 36), or an empty array.
Write every string in Persian.
""";

        string raw;
        try
        {
            raw = await _ai.CompleteJsonAsync(prompt, new[] { (card.ContentType ?? "image/jpeg", bytes) }, cancellationToken);
        }
        catch (AiException e)
        {
            return StatusCode(e.HttpStatus, new { success = false, message = e.UserMessage, detail = e.Detail });
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return Ok(new { success = true, extraction = doc.RootElement.Clone() });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { success = false, message = "فرمتِ نتیجهٔ استخراج معتبر نبود." });
        }
    }
}
