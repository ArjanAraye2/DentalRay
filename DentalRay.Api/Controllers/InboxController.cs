using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // ============================================================
    // The SMS inbox: phones report what they received, Dentix works out who it
    // belongs to, and the secretary clears anything that is not certain.
    //
    // Receiving is the only anonymous endpoint here and it authenticates with
    // the paired phone's own key, so a stranger on the LAN cannot inject rows.
    // ============================================================
    [ApiController]
    [Route("api/inbox")]
    public class InboxController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        private readonly SmsInboxService _inbox;

        public InboxController(DentalRayDbContext db, SmsInboxService inbox)
        {
            _db = db;
            _inbox = inbox;
        }

        public sealed record InboxText(string? Body, string? Sender, DateTime? ReceivedAt);
        public sealed record ReceiveRequest(string? DeviceToken, List<InboxText>? Messages);
        public sealed record ConfirmRequest(int PatientID);

        /// <summary>Forwarded by the Dentix phone app. One call may carry many SMS.</summary>
        [AllowAnonymous]
        [HttpPost("/api/inbox/sms")]
        public async Task<IActionResult> Receive(ReceiveRequest request, CancellationToken cancellationToken)
        {
            string token = Request.Headers["X-Dentix-Device"].FirstOrDefault() ?? request.DeviceToken ?? string.Empty;
            var device = await _db.PairedDevices.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DeviceToken == token && x.IsActive, cancellationToken);
            if (device == null) return Unauthorized(new { success = false, message = "گوشی جفت نشده است." });

            int received = 0, linked = 0, pending = 0;

            foreach (var message in request.Messages ?? new List<InboxText>())
            {
                string body = (message.Body ?? string.Empty).Trim();
                if (body.Length == 0) continue;
                if (body.Length > 2000) body = body[..2000];

                var links = _inbox.ExtractLinks(body);
                var match = await _inbox.MatchAsync(body, message.Sender, cancellationToken);

                var row = new InboxMessage
                {
                    DeviceID = device.DeviceID,
                    SenderMobile = SmsInboxService.NormalizeMobile(message.Sender),
                    Body = body,
                    Links = links.Count == 0 ? null : string.Join("\n", links),
                    ReceivedDate = message.ReceivedAt ?? DateTime.Now,
                    PatientID = match.PatientID,
                    MatchMethod = match.Method,
                    Status = match.PatientID.HasValue ? (byte)1 : (byte)0,
                    Note = match.Note
                };

                // A sure match goes straight to the record; anything guessed
                // waits for the secretary.
                if (row.PatientID.HasValue && links.Count > 0)
                {
                    int fetched = 0, imported = 0;
                    foreach (string link in links)
                    {
                        var files = await _inbox.FetchSharedImagesAsync(link, cancellationToken);
                        fetched += files.Count;
                        imported += await _inbox.ImportToPatientAsync(row.PatientID.Value, files, cancellationToken);
                    }
                    row.ImportedCount = imported;
                    row.Note = DescribeImport(links.Count, fetched, imported);
                }
                else if (row.PatientID.HasValue && links.Count == 0)
                {
                    row.Note = "پیامک بدون لینک؛ فقط در پروندهٔ بیمار ثبت شد.";
                }

                _db.InboxMessages.Add(row);
                received++;
                if (row.Status == 1) linked++; else pending++;
            }

            await _db.SaveChangesAsync(cancellationToken);

            device.LastSeenDate = DateTime.Now;
            try { await _db.SaveChangesAsync(cancellationToken); } catch { /* seen-time is a nicety */ }

            return Ok(new { success = true, received, linked, pending });
        }

        /// <summary>The queue the secretary works through.</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] byte? status, CancellationToken cancellationToken)
        {
            var query = _db.InboxMessages.AsNoTracking();
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);

            var items = await (
                from message in query
                join patient in _db.Patients.AsNoTracking() on message.PatientID equals patient.PatientID into patients
                from patient in patients.DefaultIfEmpty()
                orderby message.CreatedDate descending
                select new
                {
                    message.MessageID, message.SenderMobile, message.Body, message.Links,
                    message.ReceivedDate, message.PatientID, message.MatchMethod, message.Status,
                    message.ImportedCount, message.Note, message.CreatedDate,
                    PatientName = patient == null ? null : (patient.FirstName + " " + patient.LastName)
                }).Take(200).ToListAsync(cancellationToken);

            return Ok(new { success = true, items });
        }

        /// <summary>Attach an unsure message to the patient the secretary picked.</summary>
        [HttpPost("{messageID:long}/confirm")]
        public async Task<IActionResult> Confirm(long messageID, ConfirmRequest request, CancellationToken cancellationToken)
        {
            var row = await _db.InboxMessages.FirstOrDefaultAsync(x => x.MessageID == messageID, cancellationToken);
            if (row == null) return NotFound(new { success = false, message = "پیامک پیدا نشد." });

            var patient = await _db.Patients.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PatientID == request.PatientID, cancellationToken);
            if (patient == null) return NotFound(new { success = false, message = "بیمار پیدا نشد." });

            row.PatientID = patient.PatientID;
            row.Status = 1;
            row.MatchMethod = row.MatchMethod ?? 0;

            int imported = 0, fetched = 0;
            var links = _inbox.ExtractLinks(row.Links) is { Count: > 0 } stored
                ? stored
                : _inbox.ExtractLinks(row.Body);
            foreach (string link in links)
            {
                var files = await _inbox.FetchSharedImagesAsync(link, cancellationToken);
                fetched += files.Count;
                imported += await _inbox.ImportToPatientAsync(patient.PatientID, files, cancellationToken);
            }
            row.ImportedCount = imported;
            row.Note = DescribeImport(links.Count, fetched, imported);

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, fetched, imported, message = imported > 0 ? $"{imported} تصویر وارد شد." : "تصویر جدیدی اضافه نشد." });
        }

        /// <summary>
        /// What the secretary should read: nothing found is a different problem
        /// from "found, but this patient already had every one of them".
        /// </summary>
        private static string DescribeImport(int linkCount, int fetched, int imported)
        {
            if (fetched == 0) return "لینک پیدا شد ولی تصویری قابل دریافت نبود.";
            if (imported == 0) return $"هر {fetched} تصویر دریافتی قبلاً در پروندهٔ همین بیمار بود (تکراری).";
            return $"از {linkCount} لینک، {imported} تصویر جدید وارد شد (بدون Study؛ در پرونده قابل انتخاب است).";
        }

        [HttpPost("{messageID:long}/reject")]
        public async Task<IActionResult> Reject(long messageID, CancellationToken cancellationToken)
        {
            var row = await _db.InboxMessages.FirstOrDefaultAsync(x => x.MessageID == messageID, cancellationToken);
            if (row == null) return NotFound(new { success = false, message = "پیامک پیدا نشد." });
            row.Status = 2;
            row.Note = "توسط منشی رد شد.";
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
    }
}
