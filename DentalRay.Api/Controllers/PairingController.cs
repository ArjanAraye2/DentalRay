using System.Security.Cryptography;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.AspNetCore.Authorization;
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
        private readonly IWebHostEnvironment _environment;

        public PairingController(DentalRayDbContext db, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _db = db;
            _configuration = configuration;
            _environment = environment;
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
                server,
                // The phone needs the installer file before it can pair at all.
                downloadUrl = $"{server}/download/app"
            });
        }

        /// <summary>
        /// The installer itself, so the clinic phone can fetch it straight from
        /// Dentix instead of a cable. The file is picked up from the build
        /// output, so a newer build is offered automatically.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("/download/app")]
        public IActionResult DownloadApp()
        {
            string? configured = _configuration["MobileApp:ApkPath"]?.Trim();
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured))
                candidates.Add(Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(_environment.ContentRootPath, configured));

            string root = _environment.ContentRootPath;
            candidates.Add(Path.Combine(root, "..", "DentalRay.Mobile", "dist"));
            candidates.Add(Path.Combine(root, "wwwroot", "install"));

            string? file = null;
            foreach (string directory in candidates)
            {
                if (System.IO.File.Exists(directory) && directory.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                {
                    file = directory;
                    break;
                }
                if (Directory.Exists(directory))
                {
                    file = Directory.GetFiles(directory, "*.apk")
                        .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                        .FirstOrDefault();
                    if (file != null) break;
                }
            }

            if (file == null)
                return NotFound(new
                {
                    success = false,
                    message = "فایل نصب برنامه پیدا نشد؛ ابتدا برنامهٔ اندروید را بسازید."
                });

            return PhysicalFile(file, "application/vnd.android.package-archive", "Dentix.apk", enableRangeProcessing: false);
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

        /// <summary>
        /// The address a phone can actually reach. When Dentix is opened on the
        /// server itself (localhost), the QR would hand the phone a useless
        /// address, so the machine's LAN address is used instead.
        /// </summary>
        private string BuildServerUrl()
        {
            string? publicHost = _configuration["RemoteAccess:PublicHost"]?.Trim();
            if (!string.IsNullOrWhiteSpace(publicHost))
            {
                string scheme = _configuration["RemoteAccess:LocalScheme"]?.Trim() ?? "http";
                int port = _configuration.GetValue<int?>("RemoteAccess:LocalPort") ?? 5202;
                bool defaultPort = (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
                return $"{scheme}://{publicHost}{(defaultPort ? "" : $":{port}")}";
            }

            string host = Request.Host.Host;
            bool isLoopback = host is "localhost" or "127.0.0.1" or "::1";
            if (!isLoopback) return $"{Request.Scheme}://{Request.Host}";

            try
            {
                string lan = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                    .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                         && !System.Net.IPAddress.IsLoopback(a))
                    ?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(lan))
                {
                    int port = Request.Host.Port ?? 5202;
                    return $"{Request.Scheme}://{lan}:{port}";
                }
            }
            catch { /* falls back to the request host below */ }

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
