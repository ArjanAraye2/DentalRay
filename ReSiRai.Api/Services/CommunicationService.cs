using System.Net;
using System.Text.Json;
using ReSiRai.Api.Models;

namespace ReSiRai.Api.Services
{
    public interface ICommunicationService
    {
        CommunicationChannelSettings GetSettings();
        Task SaveSettingsAsync(CommunicationChannelSettings settings);
        Task<(bool Success, string Message)> SendSmsAsync(string mobile, string message);
    }

    public sealed class CommunicationService : ICommunicationService
    {
        private readonly string _settingsPath;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CommunicationService> _logger;
        private readonly object _sync = new();

        public CommunicationService(IHttpClientFactory httpClientFactory, ILogger<CommunicationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ReSiRai");
            Directory.CreateDirectory(directory);
            _settingsPath = Path.Combine(directory, "ReSiRai.communication.json");
        }

        public CommunicationChannelSettings GetSettings()
        {
            lock (_sync)
            {
                if (!File.Exists(_settingsPath)) return new CommunicationChannelSettings();
                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonSerializer.Deserialize<CommunicationChannelSettings>(json) ?? new CommunicationChannelSettings();
                }
                catch { return new CommunicationChannelSettings(); }
            }
        }

        public async Task SaveSettingsAsync(CommunicationChannelSettings settings)
        {
            settings.SmsApiKey = (settings.SmsApiKey ?? string.Empty).Trim();
            settings.SmsApiUrl = string.IsNullOrWhiteSpace(settings.SmsApiUrl) ? "https://api.kavenegar.com/v1" : settings.SmsApiUrl.Trim().TrimEnd('/');
            settings.SmsProvider = string.IsNullOrWhiteSpace(settings.SmsProvider) ? "Kavenegar" : settings.SmsProvider.Trim();

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            lock (_sync)
            {
                try
                {
                    File.WriteAllText(_settingsPath, json);
                }
                catch (UnauthorizedAccessException ex)
                {
                    // The settings folder under ProgramData only grants write to
                    // administrators on some installations. Reporting the real
                    // reason is far more useful than a generic failure.
                    _logger.LogError(ex, "Cannot write communication settings to {Path}", _settingsPath);
                    throw new InvalidOperationException(
                        "ذخیره تنظیمات پیامک ممکن نشد؛ دسترسی نوشتن در پوشه تنظیمات وجود ندارد. برنامه را با دسترسی مدیر اجرا کنید.", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cannot write communication settings to {Path}", _settingsPath);
                    throw new InvalidOperationException("ذخیره تنظیمات پیامک ناموفق بود.", ex);
                }
            }
            await Task.CompletedTask;
        }

        public async Task<(bool Success, string Message)> SendSmsAsync(string mobile, string message)
        {
            var settings = GetSettings();
            if (!settings.SmsEnabled) return (false, "سرویس پیامک فعال نیست.");
            if (string.IsNullOrWhiteSpace(settings.SmsApiKey)) return (false, "کلید API پیامک تنظیم نشده است.");

            mobile = NormalizeMobile(mobile);
            if (mobile.Length < 10) return (false, "شماره موبایل معتبر نیست.");

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);

                if (string.Equals(settings.SmsProvider, "Kavenegar", StringComparison.OrdinalIgnoreCase))
                {
                    var endpoint = $"{settings.SmsApiUrl}/{Uri.EscapeDataString(settings.SmsApiKey)}/sms/send.json";
                    var form = new Dictionary<string, string>
                    {
                        ["receptor"] = mobile,
                        ["message"] = message
                    };
                    if (!string.IsNullOrWhiteSpace(settings.SmsSender)) form["sender"] = settings.SmsSender;
                    using var response = await client.PostAsync(endpoint, new FormUrlEncodedContent(form));
                    var body = await response.Content.ReadAsStringAsync();

                    // ارائه‌دهنده پیام را به فارسی برمی‌گرداند (مثلاً ۴۱۶ برای محدودیت IP)؛
                    // نشان دادن همان پیام به‌جای کد بی‌معنی HTTP، کاربر را سردرگم نمی‌کند.
                    int providerStatus = 0;
                    string providerMessage = string.Empty;
                    try
                    {
                        using var json = JsonDocument.Parse(body);
                        var returnValue = json.RootElement.GetProperty("return");
                        if (returnValue.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Number)
                            providerStatus = st.GetInt32();
                        if (returnValue.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                            providerMessage = msg.GetString() ?? string.Empty;
                    }
                    catch { /* برخی ارائه‌دهنده‌های سازگار به‌جای JSON پاسخ ساده می‌دهند. */ }

                    bool providerRejected = providerStatus != 0 && providerStatus != 200;
                    if (!response.IsSuccessStatusCode || providerRejected)
                        return (false, DescribeSendFailure((int)response.StatusCode, providerStatus, providerMessage));

                    return (true, "پیامک با موفقیت ارسال شد.");
                }

                return (false, $"Provider «{settings.SmsProvider}» هنوز پیاده‌سازی نشده است. تنظیمات برای توسعه‌دهنده قابل تغییر است.");
            }
            catch (Exception ex)
            {
                return (false, $"خطا در ارتباط با سرویس پیامک: {ex.Message}");
            }
        }

        // کدهای رایج ارائه‌دهنده به فارسی؛ اگر خودش پیام فارسی داشته باشد همان نمایش داده می‌شود.
        private static string DescribeSendFailure(int httpStatus, int providerStatus, string providerMessage)
        {
            if (!string.IsNullOrWhiteSpace(providerMessage)) return providerMessage.Trim();

            if (providerStatus != 0)
            {
                return providerStatus switch
                {
                    400 => "درخواست نامعتبر است؛ شماره یا متن پیام را بررسی کنید.",
                    401 => "کلید API پیامک معتبر نیست.",
                    402 => "اعتبار حساب پیامک کافی نیست.",
                    403 => "دسترسی به این سرویس مجاز نیست؛ خط فرستنده یا الگوی پیام را در پنل بررسی کنید.",
                    404 => "سرویس یا شماره مقصد پیدا نشد.",
                    416 => "IP سرور در پنل ارائه‌دهنده مجاز نیست (محدودیت IP).",
                    _ => $"سرویس پیامک درخواست را نپذیرفت. (کد {providerStatus})"
                };
            }

            return $"ارسال پیامک ناموفق بود؛ خطای ارتباطی HTTP {httpStatus}.";
        }

        private static string NormalizeMobile(string value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.StartsWith("98") && digits.Length == 12) return "0" + digits[2..];
            if (digits.StartsWith("9") && digits.Length == 10) return "0" + digits;
            return digits;
        }
    }
}
