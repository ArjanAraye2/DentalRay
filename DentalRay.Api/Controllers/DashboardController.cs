using DentalRay.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;

namespace DentalRay.Api.Controllers
{
    // Read-only operational summary for the Dashboard. Patient and Study
    // commands deliberately remain in their dedicated workspaces.
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly IConfiguration _configuration;

        public DashboardController(DentalRayDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboard()
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            int totalPatients = await _context.Patients.AsNoTracking().CountAsync();
            int activePatients = await _context.Patients.AsNoTracking().CountAsync(p => p.IsActive);
            int totalStudies = await _context.RadiologyStudies.AsNoTracking().CountAsync();
            int totalImages = await _context.RadiologyImages.AsNoTracking().CountAsync();

            int newPatientsToday = await _context.Patients.AsNoTracking()
                .CountAsync(p => p.CreatedDate >= today && p.CreatedDate < tomorrow);
            int studiesToday = await _context.RadiologyStudies.AsNoTracking()
                .CountAsync(s => s.StudyDate >= today && s.StudyDate < tomorrow);
            int patientsToday = await _context.RadiologyStudies.AsNoTracking()
                .Where(s => s.StudyDate >= today && s.StudyDate < tomorrow)
                .Select(s => s.PatientID).Distinct().CountAsync();
            int imagesToday = await _context.RadiologyImages.AsNoTracking()
                .CountAsync(i => i.CreatedDate >= today && i.CreatedDate < tomorrow);

            var recentStudies = await (
                from study in _context.RadiologyStudies.AsNoTracking()
                join patient in _context.Patients.AsNoTracking() on study.PatientID equals patient.PatientID
                join type in _context.StudyTypes.AsNoTracking() on study.StudyTypeID equals type.StudyTypeID
                orderby study.StudyDate descending
                select new
                {
                    study.StudyID, study.PatientID, study.StudyDate,
                    PatientName = patient.FirstName + " " + patient.LastName,
                    type.StudyTypeName,
                    study.BodyPart
                }).Take(5).ToListAsync();

            var recentImages = await (
                from image in _context.RadiologyImages.AsNoTracking()
                join patient in _context.Patients.AsNoTracking() on image.PatientID equals patient.PatientID
                join imageType in _context.ImageTypes.AsNoTracking() on image.ImageTypeID equals imageType.ImageTypeID into types
                from imageType in types.DefaultIfEmpty()
                orderby image.CreatedDate descending
                select new
                {
                    image.ImageID, image.PatientID, image.FileName, image.ContentType, image.CreatedDate,
                    PatientName = patient.FirstName + " " + patient.LastName,
                    ImageTypeName = imageType == null ? null : imageType.ImageTypeName
                }).Take(5).ToListAsync();

            object storage;
            try
            {
                var drive = new DriveInfo(@"D:\");
                storage = new
                {
                    available = drive.IsReady && Directory.Exists(@"D:\RadiologyData"),
                    rootPath = @"D:\RadiologyData",
                    freeBytes = drive.IsReady ? drive.AvailableFreeSpace : (long?)null,
                    totalBytes = drive.IsReady ? drive.TotalSize : (long?)null
                };
            }
            catch
            {
                storage = new { available = false, rootPath = @"D:\RadiologyData", freeBytes = (long?)null, totalBytes = (long?)null };
            }

            // LAN addresses are discovered locally and never require an Internet service.
            // A public/static address cannot be guessed safely, so it is read from the
            // optional RemoteAccess:PublicHost setting in DentalRay.config.json.
            string scheme = _configuration["RemoteAccess:LocalScheme"]?.Trim() ?? "http";
            int port = _configuration.GetValue<int?>("RemoteAccess:LocalPort") ?? Request.Host.Port ?? 5202;
            string hostName = Dns.GetHostName();
            string[] localIps;
            try
            {
                localIps = Dns.GetHostAddresses(hostName)
                    .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    .Select(address => address.ToString()).Distinct().OrderBy(address => address).ToArray();
            }
            catch (SocketException)
            {
                localIps = Array.Empty<string>();
            }
            var localUrls = localIps.Select(address => $"{scheme}://{address}:{port}").ToArray();
            string serverNameUrl = $"{scheme}://{hostName}:{port}";
            string? publicHost = _configuration["RemoteAccess:PublicHost"]?.Trim();
            string publicScheme = _configuration["RemoteAccess:PublicScheme"]?.Trim() ?? "http";
            int publicPort = _configuration.GetValue<int?>("RemoteAccess:PublicPort") ?? port;
            string? publicUrl = string.IsNullOrWhiteSpace(publicHost)
                ? null
                : $"{publicScheme}://{publicHost}{((publicScheme == "http" && publicPort == 80) || (publicScheme == "https" && publicPort == 443) ? "" : $":{publicPort}")}";
            var network = new
            {
                hostName,
                localIps,
                localUrls,
                serverNameUrl,
                currentUrl = $"{Request.Scheme}://{Request.Host}",
                publicHost,
                publicUrl,
                publicConfigured = !string.IsNullOrWhiteSpace(publicHost),
                note = "برای دسترسی از دستگاه دیگر، آن دستگاه باید در همان شبکه باشد و پورت برنامه در Firewall باز باشد."
            };

            return Ok(new
            {
                success = true,
                generatedAt = DateTime.Now,
                overall = new
                {
                    totalPatients,
                    activePatients,
                    inactivePatients = totalPatients - activePatients,
                    totalStudies,
                    totalImages
                },
                today = new { patientsToday, studiesToday, imagesToday, newPatientsToday },
                recentStudies,
                recentImages,
                system = new { databaseConnected = true, storage, network }
            });
        }
    }
}
