using DentalRay.Api.Data;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // Patient messaging: templates, sending, and the history of what was sent.
    //
    // Sending is available to any signed-in user, because reception staff send the
    // reminders. Changing the SMS provider settings stays SuperAdmin-only and lives
    // in CommunicationController.
    [ApiController]
    [Route("api/patients/{patientID:int}/messages")]
    public class PatientMessagesController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        private readonly PatientMessagingService _messaging;

        public PatientMessagesController(DentalRayDbContext db, PatientMessagingService messaging)
        {
            _db = db;
            _messaging = messaging;
        }

        public sealed class SendMessageRequest
        {
            public string? TemplateKey { get; set; }
            public string? Body { get; set; }
            public string? Mobile { get; set; }
            public int? AppointmentID { get; set; }
        }

        /// <summary>The templates the UI offers, so wording stays in one place.</summary>
        [HttpGet("/api/messages/templates")]
        public IActionResult Templates() =>
            Ok(new
            {
                success = true,
                templates = MessageTemplates.All.Select(t => new { key = t.Key, title = t.Title, body = t.Body, containsAmount = t.ContainsAmount })
            });

        [HttpGet]
        public async Task<IActionResult> History(int patientID, [FromQuery] int take = 50)
        {
            if (!await _db.Patients.AsNoTracking().AnyAsync(p => p.PatientID == patientID))
                return NotFound(new { success = false, message = "بیمار پیدا نشد." });

            take = Math.Clamp(take, 1, 200);
            var rows = await _db.PatientMessages.AsNoTracking()
                .Where(m => m.PatientID == patientID)
                .OrderByDescending(m => m.CreatedDate)
                .Take(take)
                .Select(m => new
                {
                    m.MessageID, m.Mobile, m.Body, m.TemplateKey,
                    m.Status, m.ErrorMessage, m.SentAt, m.CreatedDate
                })
                .ToListAsync();

            return Ok(new { success = true, messages = rows });
        }

        [HttpPost]
        public async Task<IActionResult> Send(int patientID, SendMessageRequest request, CancellationToken cancellationToken)
        {
            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "بیمار پیدا نشد." });

            var mobile = string.IsNullOrWhiteSpace(request.Mobile)
                ? patient.Mobile
                : request.Mobile.Trim();

            // Build the text: a chosen template wins, otherwise the free text is used.
            string body;
            string? templateKey = request.TemplateKey;
            if (!string.IsNullOrWhiteSpace(request.Body))
            {
                body = request.Body.Trim();
            }
            else
            {
                var template = MessageTemplates.Find(templateKey);
                if (template == null) return BadRequest(new { success = false, message = "متن پیام را وارد کنید." });
                body = template.Body;
            }

            if (string.IsNullOrWhiteSpace(body)) return BadRequest(new { success = false, message = "متن پیام خالی است." });
            if (body.Length > 2000) return BadRequest(new { success = false, message = "متن پیام نمی‌تواند بیشتر از ۲۰۰۰ نویسه باشد." });

            // A template may reference the appointment time.
            DateTime? when = null;
            if (request.AppointmentID.HasValue)
            {
                var appt = await _db.Appointments.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AppointmentID == request.AppointmentID.Value && a.PatientID == patientID, cancellationToken);
                if (appt == null) return BadRequest(new { success = false, message = "نوبت پیدا نشد." });
                when = appt.AppointmentDate;
            }

            string rendered = PatientMessagingService.RenderBody(body, when)
                .Replace("{patient}", $"{patient.FirstName} {patient.LastName}".Trim());

            var outcome = await _messaging.SendAsync(patientID, mobile ?? string.Empty, rendered,
                templateKey, request.AppointmentID, null, cancellationToken);

            if (!outcome.Success)
                return BadRequest(new { success = false, message = outcome.Message, messageID = outcome.MessageID });

            return Ok(new { success = true, message = outcome.Message, messageID = outcome.MessageID });
        }
    }
}
