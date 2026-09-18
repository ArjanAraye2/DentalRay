using DentalRay.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // Read-only operational summary for the Dashboard. Patient and Study
    // commands deliberately remain in their dedicated workspaces.
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly DentalRayDbContext _context;

        public DashboardController(DentalRayDbContext context)
        {
            _context = context;
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
                system = new { databaseConnected = true, storage }
            });
        }
    }
}
