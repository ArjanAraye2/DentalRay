using System.Security.Cryptography;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // Pairing a phone with Dentix.
    //
    // The clinic picks "pair a phone" and the screen shows a QR code; the
    // Dentix app on the phone scans it and starts forwarding. The QR carries
    // the server address plus a random key, so consent is a deliberate act by
    // whoever holds the phone and no patient secret ever travels.
    [ApiController]
    [Route("api/pairing")]
    public class PairingController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        private readonly IConfiguration _configuration;

        public PairingController(DentalRayDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        public sealed record CreatePairingRequest(string? Label, byte OwnerKind, int? PatientID);

        [HttpPost]
        public async Task<IActionResult> Create(CreatePairingRequest request, CancellationToken cancellationToken)
        {
            byte ownerKind = request.OwnerKind == 2 ? (byte)2 : (byte)1;
            int? patientID = null;
            string label = string.IsNullOrWhiteSpace(request.Label) ? string.Empty : request.Label.Trim();

            if (ownerKind == 2)
            {
                if (!request.PatientID.HasValue)
                    return BadRequest(new { success = false, message = "برای گوشی بیمار، شناسهٔ بیمار لازم است." });
                var patient = await _db.Patients.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.PatientID == request.PatientID.Value, cancellationToken);
                if (patient == null) return NotFound(new { success = false, message = "بیمار پیدا نشد." });
                patientID = patient.PatientID;
                if (string.IsNullOrWhiteSpace(label))
                    label = $"موبایل {patient.FirstName} {patient.LastName}".Trim();
            }
            if (string.IsNullOrWhiteSpace(label)) label = "موبایل مطب";

            var device = new PairedDevice
            {
                DeviceToken = NewToken(),
                Label = label,
                OwnerKind = ownerKind,
                PatientID = patientID,
                PairedDate = DateTime.Now,
                IsActive = true
            };
            _db.PairedDevices.Add(device);
            await _db.SaveChangesAsync(cancellationToken);

            string server = BuildServerUrl();
            // The payload the app scans: where to report and with which key.
            string payload = $"dentix://pair?server={Uri.EscapeDataString(server)}&token={device.DeviceToken}";

            return Ok(new
            {
                success = true,
                device.DeviceID,
                device.Label,
                qr = payload,
                server
            });
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var items = await _db.PairedDevices.AsNoTracking()
                .OrderByDescending(x => x.PairedDate)
                .Select(x => new
                {
                    x.DeviceID, x.Label, x.OwnerKind, x.PatientID, x.PairedDate, x.LastSeenDate, x.IsActive
                })
                .ToListAsync(cancellationToken);
            return Ok(new { success = true, items });
        }

        [HttpDelete("{deviceID:int}")]
        public async Task<IActionResult> Revoke(int deviceID, CancellationToken cancellationToken)
        {
            var device = await _db.PairedDevices.FirstOrDefaultAsync(x => x.DeviceID == deviceID, cancellationToken);
            if (device == null) return NotFound(new { success = false, message = "گوشی پیدا نشد." });
            device.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, message = "دسترسی گوشی قطع شد." });
        }

        private string BuildServerUrl()
        {
            string? publicHost = _configuration["RemoteAccess:PublicHost"]?.Trim();
            if (!string.IsNullOrWhiteSpace(publicHost))
            {
                string scheme = _configuration["RemoteAccess:PublicScheme"]?.Trim() ?? "http";
                int port = _configuration.GetValue<int?>("RemoteAccess:PublicPort") ?? 5202;
                bool defaultPort = (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
                return $"{scheme}://{publicHost}{(defaultPort ? "" : $":{port}")}";
            }
            return $"{Request.Scheme}://{Request.Host}";
        }

        private static string NewToken()
        {
            Span<byte> bytes = stackalloc byte[24];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
