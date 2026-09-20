using System.Globalization;
using DentalRay.Api.Data;
using DentalRay.Api.Models;

namespace DentalRay.Api.Services
{
    /// <summary>
    /// Sends patient messages and records every attempt.
    ///
    /// The log matters as much as the send: when a patient says the message never
    /// arrived, the clinic needs to show what was sent and when. Nothing here
    /// throws on a provider failure - the failure is stored and returned so the
    /// caller can decide what to tell the user.
    /// </summary>
    public sealed class PatientMessagingService
    {
        private readonly DentalRayDbContext _db;
        private readonly ICommunicationService _communication;
        private readonly ILogger<PatientMessagingService> _logger;

        public PatientMessagingService(DentalRayDbContext db, ICommunicationService communication,
            ILogger<PatientMessagingService> logger)
        {
            _db = db;
            _communication = communication;
            _logger = logger;
        }

        /// <summary>Fills the date and time placeholders using the Persian calendar.</summary>
        public static string RenderBody(string body, DateTime? when = null)
        {
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;
            var date = when ?? DateTime.Now;
            var pc = new PersianCalendar();
            string jalali = $"{pc.GetYear(date):0000}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}";
            string time = $"{date.Hour:00}:{date.Minute:00}";
            return body.Replace("{date}", jalali).Replace("{time}", time);
        }

        public sealed record SendOutcome(bool Success, string Message, long? MessageID);

        /// <summary>
        /// Sends a message to a patient and stores the outcome.
        /// </summary>
        public async Task<SendOutcome> SendAsync(
            int patientID,
            string mobile,
            string body,
            string? templateKey = null,
            int? appointmentID = null,
            int? createdBy = null,
            CancellationToken cancellationToken = default)
        {
            var row = new PatientMessage
            {
                PatientID = patientID,
                Mobile = mobile ?? string.Empty,
                Body = body ?? string.Empty,
                TemplateKey = templateKey,
                AppointmentID = appointmentID,
                CreatedBy = createdBy,
                CreatedDate = DateTime.Now,
                Status = 0
            };

            if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(body))
            {
                row.Status = 2;
                row.ErrorMessage = string.IsNullOrWhiteSpace(mobile) ? "شماره موبایل ثبت نشده است." : "متن پیام خالی است.";
                _db.PatientMessages.Add(row);
                await _db.SaveChangesAsync(cancellationToken);
                return new SendOutcome(false, row.ErrorMessage!, row.MessageID);
            }

            var result = await _communication.SendSmsAsync(mobile, body);

            row.Status = (byte)(result.Success ? 1 : 2);
            row.SentAt = result.Success ? DateTime.Now : null;
            row.ErrorMessage = result.Success ? null : Truncate(result.Message, 500);

            _db.PatientMessages.Add(row);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // The message may already have gone out; never fail the caller over
                // a logging problem, but make it visible.
                _logger.LogError(ex, "Could not store the message log for patient {PatientID}", patientID);
            }

            return new SendOutcome(result.Success, result.Message, row.MessageID);
        }

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);
    }
}
