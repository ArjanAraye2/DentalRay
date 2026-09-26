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
        public sealed record ImportLinkRequest(string? Url);

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

                // همان پیامک ممکن است چند بار از گوشی برسد (خواندن دوبارهٔ صندوق
                // یا اتصال قطع و وصل شده). با ترکیب «فرستنده + متن + تاریخ دریافت»
                // فقط یک بار ثبت می‌شود تا فهرست شلوغ و آمار گمراه‌کننده نشود.
                DateTime receivedAt = message.ReceivedAt ?? DateTime.Now;
                bool seen = await _db.InboxMessages.AsNoTracking().AnyAsync(x =>
                    x.DeviceID == device.DeviceID && x.Body == body && x.ReceivedDate == receivedAt, cancellationToken);
                if (seen) continue;

                var links = _inbox.ExtractLinks(body);
                var match = await _inbox.MatchAsync(body, message.Sender, cancellationToken);

                var row = new InboxMessage
                {
                    DeviceID = device.DeviceID,
                    SenderMobile = SmsInboxService.NormalizeMobile(message.Sender),
                    Body = body,
                    Links = links.Count == 0 ? null : string.Join("\n", links),
                    ReceivedDate = receivedAt,
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
                    int? studyID = null;
                    foreach (string link in links)
                    {
                        var files = await _inbox.FetchSharedImagesAsync(link, cancellationToken);
                        fetched += files.Count;
                        var result = await _inbox.ImportToPatientAsync(row.PatientID.Value, files, studyID: null, cancellationToken);
                        imported += result.Imported;
                        studyID ??= result.StudyID;
                    }
                    row.ImportedCount = imported;
                    row.Note = DescribeImport(links.Count, fetched, imported, studyID);
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

        /// <summary>
        /// سناریوی ۳: لینک از قبل در دست است (کپی‌شده از پیامک یا ایمیل) و منشی
        /// می‌خواهد تصاویر آن را داخل همین Study بگیرد. همان موتور دریافت، با
        /// همان قواعد: حذف تصویر تکراری و پیام شفاف.
        /// </summary>
        [HttpPost("/api/studies/{studyID:int}/import-link")]
        public async Task<IActionResult> ImportLink(int studyID, ImportLinkRequest request, CancellationToken cancellationToken)
        {
            string text = (request.Url ?? string.Empty).Trim();
            if (text.Length == 0)
                return BadRequest(new { success = false, message = "لینک را وارد کنید." });

            var study = await _db.RadiologyStudies.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudyID == studyID, cancellationToken);
            if (study == null) return NotFound(new { success = false, message = "Study پیدا نشد." });

            var links = _inbox.ExtractLinks(text);
            if (links.Count == 0)
                return BadRequest(new { success = false, message = "در متن واردشده آدرسی با http پیدا نشد." });

            int fetched = 0, imported = 0;
            foreach (string link in links)
            {
                var files = await _inbox.FetchSharedImagesAsync(link, cancellationToken);
                fetched += files.Count;
                var result = await _inbox.ImportToPatientAsync(study.PatientID, files, studyID, cancellationToken);
                imported += result.Imported;
            }

            string message = fetched == 0
                ? "لینک پیدا شد ولی تصویری قابل دریافت نبود."
                : imported > 0
                    ? $"{imported} تصویر دریافت و به Study شمارهٔ {studyID} وصل شد."
                    : $"هر {fetched} تصویر قبلاً در پرونده بود و حالا در همین Study نمایش داده می‌شود.";

            // سابقه در همان صندوق ورودی، با برچسب «دستی»
            _db.InboxMessages.Add(new InboxMessage
            {
                DeviceID = null,
                Source = 3,
                Body = text.Length > 2000 ? text[..2000] : text,
                Links = string.Join("\n", links),
                ReceivedDate = DateTime.Now,
                PatientID = study.PatientID,
                MatchMethod = 0,
                Status = 1,
                ImportedCount = imported,
                Note = message,
                CreatedDate = DateTime.Now
            });
            try { await _db.SaveChangesAsync(cancellationToken); } catch { /* سابقه فرعی است */ }

            return Ok(new { success = true, fetched, imported, studyID, message });
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
            int? studyID = null;
            var links = _inbox.ExtractLinks(row.Links) is { Count: > 0 } stored
                ? stored
                : _inbox.ExtractLinks(row.Body);
            foreach (string link in links)
            {
                var files = await _inbox.FetchSharedImagesAsync(link, cancellationToken);
                fetched += files.Count;
                var result = await _inbox.ImportToPatientAsync(patient.PatientID, files, studyID: null, cancellationToken);
                imported += result.Imported;
                studyID ??= result.StudyID;
            }
            row.ImportedCount = imported;
            row.Note = DescribeImport(links.Count, fetched, imported, studyID);

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, fetched, imported, studyID, message = imported > 0 ? $"{imported} تصویر وارد شد." : "تصویر جدیدی اضافه نشد." });
        }

        /// <summary>
        /// What the secretary should read: nothing found is a different problem
        /// from "found, but this patient already had every one of them".
        /// </summary>
        private static string DescribeImport(int linkCount, int fetched, int imported, int? studyID)
        {
            if (fetched == 0) return "لینک پیدا شد ولی تصویری قابل دریافت نبود.";
            if (imported == 0 && studyID == null)
                return $"هر {fetched} تصویر دریافتی قبلاً در پروندهٔ همین بیمار بود (تکراری).";
            string where = studyID.HasValue ? $" و به Study شمارهٔ {studyID} وصل شد" : " (بدون Study)";
            if (imported == 0)
                return $"هر {fetched} تصویر قبلاً در پرونده بود و در Study شمارهٔ {studyID ?? 0} نمایش داده می‌شود.";
            return $"از {linkCount} لینک، {imported} تصویر جدید وارد شد{where}.";
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

        /// <summary>
        /// سناریوی ۱ - بخش «ب»: بیمار پیامک را به گوشی مطب فوروارد کرده و منشی
        /// داخل همان Study ایستاده است. هر آنچه برای این بیمار آمده برمی‌داشته
        /// می‌شود و فقط به همین Study می‌چسبد (تکراری‌ها کپی نمی‌شوند).
        /// </summary>
        [HttpPost("/api/studies/{studyID:int}/pull-inbox")]
        public async Task<IActionResult> PullForStudy(int studyID, CancellationToken cancellationToken)
        {
            var study = await _db.RadiologyStudies.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudyID == studyID, cancellationToken);
            if (study == null) return NotFound(new { success = false, message = "Study پیدا نشد." });

            var rows = await _db.InboxMessages.AsNoTracking()
                .Where(x => x.PatientID == study.PatientID && x.Links != null)
                .OrderByDescending(x => x.CreatedDate)
                .Take(20)
                .ToListAsync(cancellationToken);

            int imported = 0, fetched = 0, handled = 0;
            foreach (var row in rows)
            {
                var links = _inbox.ExtractLinks(row.Links);
                if (links.Count == 0) continue;
                handled++;
                foreach (string link in links)
                {
                    var files = await _inbox.FetchSharedImagesAsync(link, cancellationToken);
                    fetched += files.Count;
                    var result = await _inbox.ImportToPatientAsync(study.PatientID, files, studyID, cancellationToken);
                    imported += result.Imported;
                }
            }

            string message = handled == 0
                ? "هنوز پیامک دریافتی‌ای برای این بیمار وجود ندارد."
                : imported > 0
                    ? $"{imported} تصویر دریافت و به همین Study وصل شد."
                    : $"هر {fetched} تصویر قبلاً در پرونده بود و حالا در همین Study نمایش داده می‌شود.";

            return Ok(new { success = true, handled, fetched, imported, studyID, message });
        }
    }
}
