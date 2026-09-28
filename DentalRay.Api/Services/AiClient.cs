using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DentalRay.Api.Services;

// یک خطای قابلِ نمایش برای کاربر — پیام فارسی است چون خواننده دندان‌پزشک است، نه توسعه‌دهنده.
public sealed class AiException : Exception
{
    public AiException(string userMessage, int httpStatus = 502, string? detail = null, bool retryable = false)
        : base(userMessage)
    {
        UserMessage = userMessage;
        HttpStatus = httpStatus;
        Detail = detail;
        Retryable = retryable;
    }

    public string UserMessage { get; }
    public int HttpStatus { get; }
    public string? Detail { get; }
    /// <summary>سرویسِ رایگان گاهی می‌آویزد؛ در این حالت ارزش دارد مدلِ دیگر را امتحان کنیم.</summary>
    public bool Retryable { get; }
}

// تنها جایی که با مدلِ بینایی حرف می‌زند.
//
// اینکه کدام سرویس کارِ تحلیل را بکند، **کانفیگ است نه کد**: AI:BaseUrl ،AI:ApiKey و
// AI:Model می‌توانند به OpenRouter یا Hugging Face یا SiliconFlow یا خودِ OpenAI اشاره کنند.
// اگر نخستین مدل جواب ندهد (آویزان شدن، خطای سرور) یک بار با AI:FallbackModel امتحان
// می‌شود، چون سرویسِ رایگان گاهی یک مدل را برای دقایقی از دست می‌دهد.
public sealed class AiClient
{
    private const int AttemptTimeoutSeconds = 90;

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClients;

    public AiClient(IConfiguration configuration, IHttpClientFactory httpClients)
    {
        _configuration = configuration;
        _httpClients = httpClients;
    }

    private static string? First(params string?[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return null;
    }

    public string? ApiKey => First(_configuration["AI:ApiKey"], _configuration["OpenAI:ApiKey"],
        Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    public string BaseUrl => (First(_configuration["AI:BaseUrl"], _configuration["OpenAI:BaseUrl"])
        ?? "https://api.openai.com/v1").TrimEnd('/');

    public string? Model => First(_configuration["AI:Model"], _configuration["OpenAI:Model"]);

    /// <summary>مدلِ جایگزین وقتی مدلِ اصلی جواب نمی‌دهد.</summary>
    public string? FallbackModel => First(_configuration["AI:FallbackModel"]);

    // نبودِ کلید، ایرادِ راه‌اندازی است نه خطای سرویس ⇒ فراخواننده 503 برمی‌گرداند.
    public string? ConfigurationError()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            return "کلیدِ هوش مصنوعی تنظیم نشده است. ساختِ کلیدِ رایگان: openrouter.ai/keys و سپس AI:ApiKey در DentalRay.config.json";
        if (string.IsNullOrWhiteSpace(Model))
            return "مدلِ هوش مصنوعی تنظیم نشده است (AI:Model در DentalRay.config.json).";
        return null;
    }

    // پرامپت + تصویرها را می‌فرستد و **متنِ JSON آمادهٔ پارس** برمی‌گرداند.
    public async Task<string> CompleteJsonAsync(string prompt, IReadOnlyList<(string Mime, byte[] Bytes)> images,
        CancellationToken cancellationToken)
    {
        string? configError = ConfigurationError();
        if (configError is not null) throw new AiException(configError, 503);

        var attempts = new List<string?>();
        if (!string.IsNullOrWhiteSpace(Model)) attempts.Add(Model);
        if (!string.IsNullOrWhiteSpace(FallbackModel) &&
            !attempts.Any(m => string.Equals(m, FallbackModel, StringComparison.OrdinalIgnoreCase)))
            attempts.Add(FallbackModel);

        AiException? last = null;
        string raw = string.Empty;
        foreach (var model in attempts)
        {
            try
            {
                raw = await SendCoreAsync(prompt, images, model, cancellationToken);
                break;
            }
            catch (AiException e) when (e.Retryable)
            {
                last = e; // مدلِ بعدی را امتحان می‌کنیم
            }
        }
        if (raw.Length == 0)
            throw last ?? new AiException("ارتباط با سرویسِ هوش مصنوعی برقرار نشد.", 502);

        string text = ExtractText(raw) ?? string.Empty;
        return NormalizeJson(text);
    }

    // یک مدل: با JSON-mode، و اگر مدل آن را نداشت، بدونِ آن.
    private async Task<string> SendCoreAsync(string prompt, IReadOnlyList<(string Mime, byte[] Bytes)> images,
        string? model, CancellationToken cancellationToken)
    {
        try
        {
            return await SendAsync(BuildBody(prompt, images, model, jsonMode: true), cancellationToken);
        }
        catch (AiException e) when (e.HttpStatus == 400 && e.Detail is not null &&
                                    e.Detail.Contains("response_format", StringComparison.OrdinalIgnoreCase))
        {
            // بعضی مدل‌های رایگان خروجیِ ساختاریافته ندارند؛ همان درخواست را بدونِ آن می‌فرستیم.
            return await SendAsync(BuildBody(prompt, images, model, jsonMode: false), cancellationToken);
        }
    }

    private object BuildBody(string prompt, IReadOnlyList<(string Mime, byte[] Bytes)> images, string? model,
        bool jsonMode)
    {
        var content = new List<object> { new { type = "text", text = prompt } };
        foreach (var (mime, bytes) in images)
        {
            if (bytes is not { Length: > 0 }) continue;
            string type = string.IsNullOrWhiteSpace(mime) || !mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg" : mime;
            content.Add(new { type = "image_url", image_url = new { url = $"data:{type};base64,{Convert.ToBase64String(bytes)}" } });
        }

        var messages = new object[] { new { role = "user", content } };
        return jsonMode
            ? new { model, temperature = 0.1, response_format = new { type = "json_object" }, messages }
            : new { model, temperature = 0.1, messages };
    }

    private async Task<string> SendAsync(object body, CancellationToken cancellationToken)
    {
        var client = _httpClients.CreateClient();
        // سرویسِ رایگان گاهی دقایقی می‌آویزد؛ بیش از این ارزشِ منتظر ماندن ندارد چون
        // مدلِ جایگزین در همین زمان امتحان می‌شود.
        client.Timeout = TimeSpan.FromSeconds(AttemptTimeoutSeconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw Translate(response.StatusCode, raw);
            return raw;
        }
        catch (AiException) { throw; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiException(
                "پاسخِ سرویسِ هوش مصنوعی دیر رسید. چند لحظه بعد دوباره تلاش کنید.", 504, retryable: true);
        }
        catch (HttpRequestException e)
        {
            throw new AiException("ارتباط با سرویسِ هوش مصنوعی برقرار نشد. اتصال اینترنت را بررسی کنید.",
                502, e.Message, retryable: true);
        }
    }

    private static AiException Translate(HttpStatusCode status, string raw)
    {
        string detail = raw.Length > 500 ? raw[..500] : raw;
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AiException(
                "کلیدِ هوش مصنوعی نامعتبر است یا دسترسی ندارد. کلید را در DentalRay.config.json بررسی کنید.", 502, detail),
            HttpStatusCode.PaymentRequired => new AiException(
                "اعتبارِ حسابِ هوش مصنوعی تمام شده است.", 502, detail),
            HttpStatusCode.TooManyRequests => new AiException(
                "سقفِ استفادهٔ رایگان تمام شد. کمی بعد دوباره تلاش کنید یا مدلِ دیگری انتخاب کنید.", 429, detail),
            HttpStatusCode.BadRequest => new AiException(
                "درخواست توسط سرویسِ هوش مصنوعی پذیرفته نشد.", 502, detail),
            _ => new AiException("سرویسِ هوش مصنوعی پاسخ موفق نداد.", 502, detail,
                retryable: (int)status >= 500)
        };
    }

    private static string? ExtractText(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return null;
            if (!choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content)) return null;

            if (content.ValueKind == JsonValueKind.String) return content.GetString();
            if (content.ValueKind != JsonValueKind.Array) return null;

            var sb = new StringBuilder();
            foreach (var part in content.EnumerateArray())
                if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                    sb.Append(text.GetString());
            return sb.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // مدل‌ها گاهی ```json می‌نویسند یا متنِ اضافه دورِ JSON می‌گذارند؛ در هر دو حالت، خودِ JSON را درمی‌آوریم.
    private static string NormalizeJson(string text)
    {
        string t = text.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = t.IndexOf('\n');
            if (firstNewline >= 0)
            {
                t = t[(firstNewline + 1)..];
                int fence = t.LastIndexOf("```", StringComparison.Ordinal);
                t = (fence >= 0 ? t[..fence] : t).Trim();
            }
        }
        if (t.StartsWith("{", StringComparison.Ordinal) || t.StartsWith("[", StringComparison.Ordinal)) return t;

        int open = t.IndexOfAny(['{', '[']);
        if (open < 0) return t;
        char close = t[open] == '{' ? '}' : ']';
        int end = t.LastIndexOf(close);
        return end > open ? t[open..(end + 1)] : t;
    }
}
