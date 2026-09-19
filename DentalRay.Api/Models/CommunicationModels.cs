using System.ComponentModel.DataAnnotations;

namespace DentalRay.Api.Models
{
    public sealed class CommunicationChannelSettings
    {
        public string SmsProvider { get; set; } = "Kavenegar";
        public string SmsApiUrl { get; set; } = "https://api.kavenegar.com/v1";
        public string SmsApiKey { get; set; } = string.Empty;
        public string SmsSender { get; set; } = string.Empty;
        public string SmsOtpTemplate { get; set; } = string.Empty;
        public bool SmsEnabled { get; set; }
        public bool PushEnabled { get; set; }
        public bool EmailEnabled { get; set; }
        public bool WhatsAppEnabled { get; set; }
        public bool TelegramEnabled { get; set; }
    }

    public sealed class SendSmsRequest
    {
        public int? PatientID { get; set; }
        [Required, MaxLength(100)]
        public string Mobile { get; set; } = string.Empty;
        [Required, MaxLength(2000)]
        public string Message { get; set; } = string.Empty;
    }
}
