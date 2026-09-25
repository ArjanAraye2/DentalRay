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

        /// <summary>A contact that did not go through a provider, such as a phone call.</summary>
        public sealed class LogContactRequest
        {
            /// <summary>2 = phone call, 3 = in person, 4 = other.</summary>
            public byte Channel { get; set; } = Models.PatientContactChannel.PhoneCall;
            public byte? Outcome { get; set; }
            public int? DurationMinutes { get; set; }
            public string? Body { get; set; }
            public int? AppointmentID { get; set; }
        }

        public sealed class SendMessageRequest
        {
            public string? TemplateKey { get; set; }
            public string? Body { get; set; }
            public string? Mobile { get; set; }
            public int? AppointmentID { get; set; }
        }

        /// <summary>
        /// The current user as a user id plus a display name. The built-in
        /// SuperAdmin account has UserID 0 and no staff record, so the claims carry
        /// the name.
        /// </summary>
        private (int? UserID, string? Name) CurrentUser()
        {
            int.TryParse(User.FindFirst("UserID")?.Value, out int userID);
            string name = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();
            string userName = User.Identity?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) name = userName;
            return (userID > 0 ? userID : null, string.IsNullOrWhiteSpace(name) ? null : name);
        }

        /// <summary>The templates the UI offers, so wording stays in one place.</summary>
        [HttpGet("/api/messages/templates")]
        public IActionResult Templates() =>
            Ok(new
            {
                // share-images is excluded: its {link} only means something when
                // a link has just been issued, so it lives in the share dialog.
                success = true,
                templates = MessageTemplates.All
                    .Where(t => t.Key != "share-images")
                    .Select(t => new { key = t.Key, title = t.Title, body = t.Body, containsAmount = t.ContainsAmount })
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
                    m.Status, m.ErrorMessage, m.SentAt, m.CreatedDate,
                    m.Channel, m.Outcome, m.ContactedByName, m.DurationMinutes
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

            var (userID, userName) = CurrentUser();
            var outcome = await _messaging.SendAsync(patientID, mobile ?? string.Empty, rendered,
                templateKey, request.AppointmentID, userID, cancellationToken, userName);

            if (!outcome.Success)
                return BadRequest(new { success = false, message = outcome.Message, messageID = outcome.MessageID });

            return Ok(new { success = true, message = outcome.Message, messageID = outcome.MessageID });
        }

        /// <summary>
        /// Records a contact that did not go through a provider: a phone call, a
        /// conversation at the desk, a note. Nothing is sent, but the history then
        /// shows who was contacted, how, and what came of it.
        /// </summary>
        [HttpPost("contact")]
        public async Task<IActionResult> LogContact(int patientID, LogContactRequest request, CancellationToken cancellationToken)
        {
            if (!await _db.Patients.AsNoTracking().AnyAsync(p => p.PatientID == patientID, cancellationToken))
                return NotFound(new { success = false, message = "بیمار پیدا نشد." });

            if (request.Channel is < 2 or > 4)
                return BadRequest(new { success = false, message = "نوع ارتباط معتبر نیست." });
            if (request.Outcome is not null and (< 1 or > 6))
                return BadRequest(new { success = false, message = "نتیجه ارتباط معتبر نیست." });

            var body = (request.Body ?? string.Empty).Trim();
            if (body.Length == 0) return BadRequest(new { success = false, message = "شرح ارتباط را وارد کنید." });
            if (body.Length > 2000) return BadRequest(new { success = false, message = "شرح ارتباط نمی‌تواند بیشتر از ۲۰۰۰ نویسه باشد." });
            if (request.DurationMinutes is < 0 or > 600)
                return BadRequest(new { success = false, message = "مدت تماس معتبر نیست." });

            var (userID, userName) = CurrentUser();
            var outcome = await _messaging.LogContactAsync(patientID, request.Channel, body,
                request.Outcome, request.DurationMinutes, userID, userName, request.AppointmentID, cancellationToken);

            return Ok(new { success = true, message = outcome.Message, messageID = outcome.MessageID });
        }
    }
}
