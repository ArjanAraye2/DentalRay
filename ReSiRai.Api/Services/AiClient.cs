using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReSiRai.Api.Services;

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
    // سقفِ زمانِ **هر** تلاش: سرویسِ رایگان برای چند تصویر گاهی بیش از سه دقیقه
    // جواب می‌دهد (اندازه‌گیری شده: 216 ثانیه)، پس پیش‌فرض بلند است و با
    // AI:TimeoutSeconds در ReSiRai.config.json قابلِ تنظیم.
    private const int DefaultAttemptTimeoutSeconds = 240;

    private int AttemptTimeoutSeconds
    {
        get
        {
            string? raw = _configuration["AI:TimeoutSeconds"];
            return int.TryParse(raw, out int seconds) && seconds > 0
                ? seconds
                : DefaultAttemptTimeoutSeconds;
        }
    }

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

    /// <summary>
    /// مدل‌های جایگزین به‌ترتیب. مدلِ رایگانِ گاهی بالادستی محدود می‌شود؛ در آن
    /// حالت مدلِ بعدیِ فهرست امتحان می‌شود تا کاربر به‌جای خطا جواب بگیرد.
    /// </summary>
    public IReadOnlyList<string> FallbackModels
    {
        get
        {
            var list = new List<string>();
            string raw = First(_configuration["AI:FallbackModels"], _configuration["AI:FallbackModel"])
                ?? string.Empty;
            foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string m = part.Trim();
                if (m.Length > 0 && !list.Contains(m, StringComparer.OrdinalIgnoreCase))
                    list.Add(m);
            }
            return list;
        }
    }

    // نبودِ کلید، ایرادِ راه‌اندازی است نه خطای سرویس ⇒ فراخواننده 503 برمی‌گرداند.
    public string? ConfigurationError()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            return "کلیدِ هوش مصنوعی تنظیم نشده است. ساختِ کلیدِ رایگان: openrouter.ai/keys و سپس AI:ApiKey در ReSiRai.config.json";
        if (string.IsNullOrWhiteSpace(Model))
            return "مدلِ هوش مصنوعی تنظیم نشده است (AI:Model در ReSiRai.config.json).";
        return null;
    }

    // پرامپت + تصویرها را می‌فرستد و **متنِ JSON آمادهٔ پارس** برمی‌گرداند.
    public async Task<string> CompleteJsonAsync(string prompt, IReadOnlyList<(string Mime, byte[] Bytes)> images,
        CancellationToken cancellationToken, bool compactImages = false)
    {
        string? configError = ConfigurationError();
        if (configError is not null) throw new AiException(configError, 503);

        // کوچک‌سازیِ فقط برایِ همین درخواست — فایلِ ذخیره‌شده دست نمی‌خورد.
        // (با حلقه و نه lambda، تا گاردِ ویندوز برایِ تحلیل‌گر هم قابلِ اثبات باشد)
        var prepared = new List<(string Mime, byte[] Bytes)>(images.Count);
        foreach (var item in images)
        {
            if (OperatingSystem.IsWindows())
                prepared.Add(PrepareForAi(item.Mime, item.Bytes, compactImages));
            else
                prepared.Add(item);
        }

        var attempts = new List<string?>();
        if (!string.IsNullOrWhiteSpace(Model)) attempts.Add(Model);
        foreach (var fallback in FallbackModels)
            if (!attempts.Any(m => string.Equals(m, fallback, StringComparison.OrdinalIgnoreCase)))
                attempts.Add(fallback);

        AiException? last = null;
        foreach (var model in attempts)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                string raw = await SendCoreAsync(prompt, prepared, model, cancellationToken);
                string text = ExtractText(raw) ?? string.Empty;
                string normalized = NormalizeJson(text);
                if (normalized.Length > 0 && (normalized[0] == '{' || normalized[0] == '['))
                {
                    // کدام مدل جواب داد و چقدر طول کشید؟ برای انتخابِ پایدارترین سرویس لازم است.
                    Console.WriteLine($"[ReSiRai AI] ok via {model ?? "default"} in {watch.ElapsedMilliseconds}ms");
                    return normalized;
                }

                // پاسخِ ۲۰۰ ولی بی‌محتوا (مدلِ استدلالی، خطا در خودِ بدنه، نتیجهٔ ناقص):
                // به‌جای دادنِ خطا به کاربر، مدلِ بعدیِ فهرست را امتحان می‌کنیم و خامه را
                // در لاگ می‌گذاریم تا بعداً قابلِ بررسی باشد.
                Console.WriteLine($"[ReSiRai AI] unusable reply from {model ?? "default"}: "
                    + raw.Substring(0, Math.Min(400, raw.Length)));
                last = new AiException("پاسخِ سرویسِ هوش مصنوعی قابلِ استفاده نبود. دوباره تلاش کنید.",
                    502, retryable: true);
            }
            catch (AiException e) when (e.Retryable)
            {
                last = e; // مدلِ بعدیِ فهرست را امتحان می‌کنیم
            }
        }
        throw last ?? new AiException("ارتباط با سرویسِ هوش مصنوعی برقرار نشد.", 502);
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

    // تصویرِ ارسالی به سرویسِ AI کوچک می‌شود تا چند مگابایت به بیرون نرود.
    // فقط نسخهٔ ارسالی تغییر می‌کند: فایلِ ذخیره‌شده در مطب با همان کیفیتِ
    // کامل باقی می‌ماند، چون کیفیتِ بالینی قابلِ معامله نیست.
    private const int MaxSide = 1536;
    private const long MaxBytes = 700L * 1024;
    // در تحلیلِ چندتصویری هر تصویر کوچک‌تر فرستاده می‌شود تا مجموعِ پیام زیرِ حدِی
    // بماند که خطوطِ ناپایدار یا سرویس آن را وسطِ ارسال قطع نکنند.
    private const int CompactMaxSide = 1024;
    private const long CompactMaxBytes = 320L * 1024;

    // فقط ویندوز؛ گاردِ زمانِ اجرا هم داخلش هست ولی تحلیل‌گرِ CA1416 آن را
    // کافی نمی‌داند، پس سطحِ متد را هم صریح می‌گوییم.
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static (string Mime, byte[] Bytes) PrepareForAi(string mime, byte[] bytes, bool compact)
    {
        int maxSide = compact ? CompactMaxSide : MaxSide;
        long maxBytes = compact ? CompactMaxBytes : MaxBytes;
        if (!OperatingSystem.IsWindows()) return (mime, bytes);
        if (bytes is not { Length: > 0 }) return (mime, bytes);
        try
        {
            using var input = new MemoryStream(bytes);
            using var source = System.Drawing.Image.FromStream(input, false, false);
            int longest = Math.Max(source.Width, source.Height);
            if (longest <= maxSide && bytes.Length <= maxBytes) return (mime, bytes);

            double scale = Math.Min(1d, (double)maxSide / longest);
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));

            using var bitmap = new System.Drawing.Bitmap(width, height);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(source, 0, 0, width, height);
            }

            using var output = new MemoryStream();
            var codec = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
            using var parameters = new System.Drawing.Imaging.EncoderParameters(1);
            parameters.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality, 82L);
            bitmap.Save(output, codec, parameters);
            var result = output.ToArray();

            Console.WriteLine(
                $"[ReSiRai AI] image {bytes.Length / 1024}KB {source.Width}x{source.Height} -> {result.Length / 1024}KB {width}x{height}");
            return ("image/jpeg", result);
        }
        catch (Exception e)
        {
            // هر خطایی در پردازش ⇒ همان نسخهٔ اصلی ارسال می‌شود؛ تحلیل مهم‌تر است.
            Console.WriteLine($"[ReSiRai AI] resize skipped: {e.Message}");
            return (mime, bytes);
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
            if (!response.IsSuccessStatusCode)
            {
                // متنِ خام خطا در لاگ می‌ماند تا بعداً دقیقاً بفهمیم کدام سقف یا کدام ارائه‌دهنده بوده است.
                Console.WriteLine($"[ReSiRai AI] HTTP {(int)response.StatusCode}: "
                    + raw.Substring(0, Math.Min(400, raw.Length)));
                throw Translate(response.StatusCode, raw);
            }
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

    /// <summary>
    /// A 403 is not always a bad key: filtering gateways and security software
    /// answer with their own "denied" text before the request ever reaches the
    /// AI provider. The doctor must not go hunting in the config for a key that
    /// is perfectly fine - so the message names the real cause whenever the body
    /// makes it clear.
    /// </summary>
    private static AiException BlockedOrKey(string detail)
    {
        bool blocked = detail.Contains("security policy", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("blocked", StringComparison.OrdinalIgnoreCase);
        return blocked
            ? new AiException(
                "اتصال به سرویسِ هوش مصنوعی قطع شده است (فیلترینگِ شبکه جلوی آن را گرفته). کلیدِ شما سالم است؛ با اتصالِ بدونِ فیلتر دوباره تلاش کنید یا کمی بعد امتحان کنید.", 502, detail)
            : new AiException(
                "کلیدِ هوش مصنوعی نامعتبر است یا دسترسی ندارد. کلید را در ReSiRai.config.json بررسی کنید.", 502, detail);
    }

    private static AiException Translate(HttpStatusCode status, string raw)
    {
        string detail = raw.Length > 500 ? raw[..500] : raw;
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => BlockedOrKey(detail),
            HttpStatusCode.PaymentRequired => new AiException(
                "اعتبارِ حسابِ هوش مصنوعی تمام شده است.", 502, detail),
            HttpStatusCode.TooManyRequests => new AiException(
                "سقفِ استفادهٔ رایگان تمام شد. کمی بعد دوباره تلاش کنید یا مدلِ دیگری انتخاب کنید.", 429, detail,
                // محدودیتِ گاهی از سویِ خودِ ارائه‌دهندهٔ مدل است نه حسابِ ما؛ پس یک
                // بار با مدلِ جایگزین هم امتحان می‌کنیم تا اگر بود حل شود.
                retryable: true),
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
