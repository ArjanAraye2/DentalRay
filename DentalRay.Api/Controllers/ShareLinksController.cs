using System.Security.Cryptography;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // Creates a "view these images" link and texts it to the patient or to the
    // referring dentist.
    //
    // Creating and sending require a login; opening the link does not (that is
    // the whole point - the patient is at home without an account). The public
    // side of the feature lives in SharedViewController.
    [ApiController]
    [Route("api/sharelinks")]
    public class ShareLinksController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        private readonly StudyAccessService _studyAccess;
        private readonly PatientMessagingService _messaging;
        private readonly IConfiguration _configuration;

        public ShareLinksController(
            DentalRayDbContext db,
            StudyAccessService studyAccess,
            PatientMessagingService messaging,
            IConfiguration configuration)
        {
            _db = db;
            _studyAccess = studyAccess;
            _messaging = messaging;
            _configuration = configuration;
        }

        public sealed record CreateShareLinkRequest(string? Mobile, string? RecipientName, int? ExpiresInDays);
        public sealed record SendShareLinkRequest(long ShareID, string? Mobile, string? RecipientName, string? Message);

        /// <summary>
        /// Issues a fresh link for a Study. A new token per send keeps old SMS
        /// messages valid while letting the clinic expire a link at will.
        /// </summary>
        [HttpPost("study/{studyID:int}")]
        public async Task<IActionResult> Create(int studyID, CreateShareLinkRequest request)
        {
            if (!await _studyAccess.CanAccessStudyAsync(studyID, User))
                return NotFound(new { success = false, message = "Study not found." });

            var link = new StudyShareLink
            {
                StudyID = studyID,
                Token = NewToken(),
                Mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim(),
                RecipientName = string.IsNullOrWhiteSpace(request.RecipientName) ? null : request.RecipientName.Trim(),
                CreatedDate = DateTime.Now,
                ExpiresDate = request.ExpiresInDays is > 0
                    ? DateTime.Now.AddDays(request.ExpiresInDays.Value)
                    : null
            };

            _db.StudyShareLinks.Add(link);
            await _db.SaveChangesAsync();

            string patientName = await _db.Patients.AsNoTracking()
                .Where(x => x.PatientID == _db.RadiologyStudies
                    .Where(s => s.StudyID == studyID)
                    .Select(s => s.PatientID).FirstOrDefault())
                .Select(x => (x.FirstName + " " + x.LastName).Trim())
                .FirstOrDefaultAsync() ?? string.Empty;
            string url = BuildShareUrl(link.Token);

            // Hand the dialog a ready-to-send sentence; the sender may rewrite it.
            string defaultMessage = (MessageTemplates.Find("share-images")?.Body ?? string.Empty)
                .Replace("{patient}", patientName)
                .Replace("{link}", url);

            return Ok(new
            {
                success = true,
                shareID = link.ShareID,
                token = link.Token,
                url,
                message = defaultMessage,
                patientName,
                link.ExpiresDate
            });
        }

        /// <summary>
        /// Texts an issued link. The message is free wording: only {patient} and
        /// {link} are filled in, so the clinic is never tied to one template.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> Send(SendShareLinkRequest request, CancellationToken cancellationToken)
        {
            var link = await _db.StudyShareLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShareID == request.ShareID, cancellationToken);
            if (link == null) return NotFound(new { success = false, message = "لینک پیدا نشد." });
            if (!await _studyAccess.CanAccessStudyAsync(link.StudyID, User))
                return NotFound(new { success = false, message = "Study not found." });

            var study = await _db.RadiologyStudies.AsNoTracking().FirstAsync(x => x.StudyID == link.StudyID, cancellationToken);
            var patient = await _db.Patients.AsNoTracking().FirstAsync(x => x.PatientID == study.PatientID, cancellationToken);
            string patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            string url = BuildShareUrl(link.Token);

            string mobile = FirstNotEmpty(request.Mobile, link.Mobile, patient.Mobile);
            if (string.IsNullOrWhiteSpace(mobile))
                return BadRequest(new { success = false, message = "شماره موبایلی برای ارسال وجود ندارد." });

            string body = FirstNotEmpty(
                request.Message,
                MessageTemplates.Find("share-images")?.Body) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(body))
                return BadRequest(new { success = false, message = "متن پیام خالی است." });
            if (body.Length > 2000)
                return BadRequest(new { success = false, message = "متن پیام نمی‌تواند بیشتر از ۲۰۰۰ نویسه باشد." });

            body = PatientMessagingService.RenderBody(body)
                .Replace("{patient}", patientName)
                .Replace("{link}", url);

            var (userID, userName) = CurrentUser();
            var outcome = await _messaging.SendAsync(study.PatientID, mobile, body, "share-images",
                appointmentID: null, createdBy: userID, cancellationToken, sentByName: userName);

            // Keep what was actually sent on the link - that is the audit trail.
            link.Mobile = mobile;
            link.RecipientName = string.IsNullOrWhiteSpace(request.RecipientName) ? link.RecipientName : request.RecipientName.Trim();
            link.Message = body;
            try { await _db.SaveChangesAsync(cancellationToken); } catch { /* never fail the send over bookkeeping */ }

            if (!outcome.Success)
                return BadRequest(new { success = false, message = outcome.Message, messageID = outcome.MessageID });

            return Ok(new { success = true, message = outcome.Message, messageID = outcome.MessageID, link = url });
        }

        /// <summary>The links issued for a Study, newest first (history/audit).</summary>
        [HttpGet("study/{studyID:int}")]
        public async Task<IActionResult> List(int studyID, CancellationToken cancellationToken)
        {
            if (!await _studyAccess.CanAccessStudyAsync(studyID, User))
                return NotFound(new { success = false, message = "Study not found." });

            var items = await _db.StudyShareLinks.AsNoTracking()
                .Where(x => x.StudyID == studyID)
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new
                {
                    x.ShareID, x.RecipientName, x.Mobile, x.CreatedDate, x.ExpiresDate,
                    x.LastViewedAt, x.ViewCount, url = "/s/" + x.Token
                })
                .Take(20)
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, items });
        }

        // 24 random bytes rendered URL-safe; unguessable and safe in an SMS body.
        private static string NewToken()
        {
            Span<byte> bytes = stackalloc byte[24];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        /// <summary>
        /// The address a patient can actually reach. RemoteAccess:PublicHost wins
        /// once the clinic has a static IP or domain; before that the request's
        /// own host works inside the clinic network.
        /// </summary>
        private string BuildShareUrl(string token)
        {
            string? publicHost = _configuration["RemoteAccess:PublicHost"]?.Trim();
            string baseUrl;
            if (!string.IsNullOrWhiteSpace(publicHost))
            {
                string scheme = _configuration["RemoteAccess:PublicScheme"]?.Trim() ?? "http";
                int port = _configuration.GetValue<int?>("RemoteAccess:PublicPort") ?? 5202;
                bool defaultPort = (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
                baseUrl = $"{scheme}://{publicHost}{(defaultPort ? "" : $":{port}")}";
            }
            else
            {
                baseUrl = $"{Request.Scheme}://{Request.Host}";
            }
            return $"{baseUrl}/s/{token}";
        }

        private static string FirstNotEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

        private (int? UserID, string? Name) CurrentUser()
        {
            int.TryParse(User.FindFirst("UserID")?.Value, out int userID);
            string name = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();
            string userName = User.Identity?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) name = userName;
            return (userID > 0 ? userID : null, string.IsNullOrWhiteSpace(name) ? null : name);
        }
    }
}
