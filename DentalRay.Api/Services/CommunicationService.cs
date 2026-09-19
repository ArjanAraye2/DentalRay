using System.Net;
using System.Text.Json;
using DentalRay.Api.Models;

namespace DentalRay.Api.Services
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
        private readonly object _sync = new();

        public CommunicationService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DentalRay");
            Directory.CreateDirectory(directory);
            _settingsPath = Path.Combine(directory, "DentalRay.communication.json");
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
            settings.SmsApiKey = settings.SmsApiKey.Trim();
            settings.SmsApiUrl = string.IsNullOrWhiteSpace(settings.SmsApiUrl) ? "https://api.kavenegar.com/v1" : settings.SmsApiUrl.Trim().TrimEnd('/');
            settings.SmsProvider = string.IsNullOrWhiteSpace(settings.SmsProvider) ? "Kavenegar" : settings.SmsProvider.Trim();
            lock (_sync)
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
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
                    if (!response.IsSuccessStatusCode) return (false, $"ارسال پیامک ناموفق بود. HTTP {(int)response.StatusCode}.");
                    try
                    {
                        using var json = JsonDocument.Parse(body);
                        var returnValue = json.RootElement.GetProperty("return");
                        var status = returnValue.GetProperty("status").GetInt32();
                        if (status != 200) return (false, "سرویس پیامک درخواست را نپذیرفت.");
                    }
                    catch { /* Some compatible providers may return a non-JSON success response. */ }
                    return (true, "پیامک با موفقیت ارسال شد.");
                }

                return (false, $"Provider «{settings.SmsProvider}» هنوز پیاده‌سازی نشده است. تنظیمات برای توسعه‌دهنده قابل تغییر است.");
            }
            catch (Exception ex)
            {
                return (false, $"خطا در ارتباط با سرویس پیامک: {ex.Message}");
            }
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
