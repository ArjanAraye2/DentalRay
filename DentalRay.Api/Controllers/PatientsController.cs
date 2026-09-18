using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly RadiologyStorageService _storageService;
        private readonly StudyAccessService _studyAccess;

        public PatientsController(DentalRayDbContext context, RadiologyStorageService storageService, StudyAccessService studyAccess)
        {
            _context = context;
            _storageService = storageService;
            _studyAccess = studyAccess;
        }

        // Patient identity/basic information is shared among authenticated DentalRay users.
        // StudyAccessService is deliberately NOT applied to Patient discovery.
        [HttpGet]
        public async Task<IActionResult> GetPatients(string? search = null, bool includeInactive = false)
        {
            // Statistics describe the complete patient population, while the list below
            // still respects the current search/include-inactive filters.
            var totalPatients = await _context.Patients.AsNoTracking().CountAsync();
            var activePatients = await _context.Patients.AsNoTracking().CountAsync(p => p.IsActive);
            var inactivePatients = totalPatients - activePatients;
            var patientsWithStudies = await _context.RadiologyStudies.AsNoTracking()
                .Select(s => s.PatientID).Distinct().CountAsync();

            var query = _context.Patients.AsNoTracking().AsQueryable();
            if (!includeInactive) query = query.Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string text = search.Trim();
                query = query.Where(p =>
                    p.NationalCode.Contains(text) ||
                    p.FirstName.Contains(text) ||
                    p.LastName.Contains(text) ||
                    (p.Mobile != null && p.Mobile.Contains(text)));
            }

            var patients = await query
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .Take(100)
                .Select(p => new
                {
                    p.PatientID, p.NationalCode, p.FirstName, p.LastName,
                    p.BirthDate, p.Gender, p.Mobile, p.IsActive,
                    p.CreatedDate, p.ModifiedDate
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                count = patients.Count,
                statistics = new { totalPatients, activePatients, inactivePatients, patientsWithStudies },
                patients
            });
        }

        [HttpGet("{nationalCode}")]
        public async Task<IActionResult> GetPatient(string nationalCode)
        {
            var patient = await _context.Patients.AsNoTracking()
                .FirstOrDefaultAsync(p => p.NationalCode == nationalCode);

            return patient == null
                ? NotFound(new { success = false, message = "Patient not found." })
                : Ok(patient);
        }

        // A normal authenticated user may register a Patient before the first Study is created.
        [HttpPost]
        public async Task<IActionResult> CreatePatient(Patient patient)
        {
            patient.NationalCode = patient.NationalCode.Trim();
            if (!IranianNationalCodeValidator.IsValid(patient.NationalCode))
                return BadRequest(new
                {
                    success = false,
                    message = "کد ملی واردشده معتبر نیست. لطفاً کد ملی ۱۰ رقمی صحیح را وارد کنید.",
                    messageEn = "The entered National Code is invalid. Please enter a valid 10-digit Iranian National Code."
                });

            if (await _context.Patients.AnyAsync(p => p.NationalCode == patient.NationalCode))
                return Conflict(new { success = false, message = "A patient with this NationalCode already exists." });

            patient.PatientID = 0;
            patient.CreatedDate = DateTime.Now;
            patient.ModifiedDate = null;
            patient.IsActive = true;
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, patient });
        }

        [HttpPut("{patientID:int}")]
        public async Task<IActionResult> UpdatePatient(int patientID, UpdatePatientRequest request)
        {
            if (patientID <= 0)
                return BadRequest(new { success = false, message = "PatientID must be greater than zero." });

            string newCode = request.NationalCode.Trim();
            if (!IranianNationalCodeValidator.IsValid(newCode))
                return BadRequest(new
                {
                    success = false,
                    message = "کد ملی واردشده معتبر نیست. لطفاً کد ملی ۱۰ رقمی صحیح را وارد کنید.",
                    messageEn = "The entered National Code is invalid. Please enter a valid 10-digit Iranian National Code."
                });

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null)
                return NotFound(new { success = false, message = "Patient not found." });

            bool codeChanged = !string.Equals(patient.NationalCode, newCode, StringComparison.Ordinal);
            if (codeChanged && await _context.Patients.AnyAsync(p => p.PatientID != patientID && p.NationalCode == newCode))
                return Conflict(new { success = false, message = "The new NationalCode already belongs to another patient. Use the Merge operation if these records represent the same patient." });

            var moved = new List<(string OldRelativePath, string NewRelativePath)>();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (codeChanged)
                {
                    var images = await _context.RadiologyImages
                        .Where(i => i.PatientID == patientID)
                        .OrderBy(i => i.SerialNumber)
                        .ToListAsync();

                    foreach (var image in images)
                    {
                        string oldPath = image.RelativePath;
                        string newPath = _storageService.MoveImageToPatient(oldPath, newCode, image.CreatedDate, image.SerialNumber);
                        moved.Add((oldPath, newPath));
                        image.FileName = Path.GetFileName(newPath);
                        image.RelativePath = newPath;
                    }
                }

                patient.NationalCode = newCode;
                patient.FirstName = request.FirstName.Trim();
                patient.LastName = request.LastName.Trim();
                patient.BirthDate = request.BirthDate;
                patient.Gender = request.Gender;
                patient.Mobile = request.Mobile;
                patient.Address = request.Address;
                patient.Description = request.Description;
                patient.ModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, patient, nationalCodeChanged = codeChanged, renamedImages = moved.Count });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                RollBackMovedFiles(moved);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Patient update failed. Database changes were rolled back and the system attempted to restore moved image files.",
                    error = ex.Message
                });
            }
        }

        [HttpPut("{patientID:int}/deactivate")]
        public async Task<IActionResult> DeactivatePatient(int patientID)
        {
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });
            if (!patient.IsActive) return Ok(new { success = true, message = "Patient is already inactive.", patientID });

            patient.IsActive = false;
            patient.ModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, patient.PatientID, patient.NationalCode, patient.IsActive, patient.ModifiedDate });
        }

        [HttpPut("{patientID:int}/activate")]
        public async Task<IActionResult> ActivatePatient(int patientID)
        {
            if (patientID <= 0)
                return BadRequest(new { success = false, message = "PatientID must be greater than zero." });

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });

            if (!patient.IsActive)
            {
                patient.IsActive = true;
                patient.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, patient.PatientID, patient.NationalCode, patient.IsActive, patient.ModifiedDate, message = "Patient activated successfully." });
        }

        [HttpGet("{patientID:int}/details")]
        public async Task<IActionResult> GetPatientDetails(int patientID)
        {
            Console.WriteLine($"[DentalRay PatientDetails] START PatientID={patientID}");
            if (patientID <= 0)
                return BadRequest(new { success = false, message = "PatientID must be greater than zero." });

            // Keep the Patient Details endpoint deliberately simple and bounded.
            // Patient identity is shared. Studies are already access-filtered here.
            // Image totals are derived from the already-loaded Study image counts,
            // so opening a patient never needs a second RadiologyImages count query.
            var patient = await _context.Patients.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null)
                return NotFound(new { success = false, message = "Patient not found." });

            var accessibleStudies = _studyAccess.ApplyAccess(
                _context.RadiologyStudies.AsNoTracking().Where(s => s.PatientID == patientID), User);

            Console.WriteLine($"[DentalRay PatientDetails] PATIENT loaded PatientID={patientID}");

            var studies = await (
                from s in accessibleStudies
                join st in _context.StudyTypes.AsNoTracking() on s.StudyTypeID equals st.StudyTypeID
                orderby s.StudyDate descending, s.StudyID descending
                select new
                {
                    s.StudyID, s.PatientID, s.StudyDate, s.StudyTypeID,
                    StudyTypeName = st.StudyTypeName,
                    s.BodyPart, s.Description, s.Report, s.CreatedDate, s.ModifiedDate
                }).ToListAsync();

            Console.WriteLine($"[DentalRay PatientDetails] STUDIES loaded Count={studies.Count}");
            var studyIDs = studies.Select(s => s.StudyID).ToList();
            var imageCounts = studyIDs.Count == 0
                ? new Dictionary<int, int>()
                : await _context.RadiologyStudyImages.AsNoTracking()
                    .Where(x => studyIDs.Contains(x.StudyID))
                    .GroupBy(x => x.StudyID)
                    .Select(g => new { StudyID = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.StudyID, x => x.Count);

            Console.WriteLine($"[DentalRay PatientDetails] IMAGE COUNTS loaded Count={imageCounts.Count}");

            var studyList = studies.Select(s => new
            {
                s.StudyID, s.PatientID, s.StudyDate, s.StudyTypeID, s.StudyTypeName,
                s.BodyPart, s.Description, s.Report, s.CreatedDate, s.ModifiedDate,
                imageCount = imageCounts.TryGetValue(s.StudyID, out int count) ? count : 0
            }).ToList();

            int totalImageCount = studyList.Sum(s => s.imageCount);

            Console.WriteLine($"[DentalRay PatientDetails] RETURN PatientID={patientID} Studies={studyList.Count} Images={totalImageCount}");

            // Diagnostic headers make the endpoint stage visible in the browser's
            // Network tab without relying on Console.WriteLine/Visual Studio Output.
            Response.Headers["X-DentalRay-PatientDetails"] = "completed";
            Response.Headers["X-DentalRay-PatientID"] = patientID.ToString();
            Response.Headers["X-DentalRay-StudyCount"] = studyList.Count.ToString();

            return Ok(new
            {
                success = true,
                patient = new
                {
                    patient.PatientID, patient.NationalCode, patient.FirstName, patient.LastName,
                    patient.BirthDate, patient.Gender, patient.Mobile, patient.Address,
                    patient.Description, patient.CreatedDate, patient.ModifiedDate, patient.IsActive
                },
                studyCount = studyList.Count,
                totalImageCount,
                studies = studyList
            });
        }

        // Merge is a Patient-level data-correction operation.
        // It is intentionally independent of Study authorization: Study ownership/scope fields
        // (ClinicID and DentistStaffID) are preserved when the duplicate Patient is merged.
        [HttpPost("merge")]
        public async Task<IActionResult> MergePatients(MergePatientRequest request)
        {
            if (request.SourcePatientID <= 0 || request.TargetPatientID <= 0 || request.SourcePatientID == request.TargetPatientID)
                return BadRequest(new { success = false, message = "SourcePatientID and TargetPatientID must be valid and different." });

            var source = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == request.SourcePatientID);
            var target = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == request.TargetPatientID);
            if (source == null) return NotFound(new { success = false, message = "Source patient not found." });
            if (target == null) return NotFound(new { success = false, message = "Target patient not found." });

            var studies = await _context.RadiologyStudies.Where(s => s.PatientID == source.PatientID).ToListAsync();
            var images = await _context.RadiologyImages.Where(i => i.PatientID == source.PatientID).OrderBy(i => i.SerialNumber).ToListAsync();

            int nextSerial = (await _context.RadiologyImages.Where(i => i.PatientID == target.PatientID)
                .Select(i => (int?)i.SerialNumber).MaxAsync() ?? 0) + 1;
            var targetSerials = (await _context.RadiologyImages.Where(i => i.PatientID == target.PatientID)
                .Select(i => i.SerialNumber).ToListAsync()).ToHashSet();
            var moved = new List<(string OldRelativePath, string NewRelativePath)>();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var image in images)
                {
                    int serial = image.SerialNumber;
                    if (serial <= 0 || targetSerials.Contains(serial)) serial = nextSerial++;
                    while (targetSerials.Contains(serial)) serial = nextSerial++;
                    targetSerials.Add(serial);

                    string oldPath = image.RelativePath;
                    string newPath = _storageService.MoveImageToPatient(oldPath, target.NationalCode, image.CreatedDate, serial);
                    moved.Add((oldPath, newPath));
                    image.PatientID = target.PatientID;
                    image.SerialNumber = serial;
                    image.FileName = Path.GetFileName(newPath);
                    image.RelativePath = newPath;
                }

                foreach (var study in studies)
                {
                    study.PatientID = target.PatientID;
                    study.ModifiedDate = DateTime.Now;
                }

                _context.Patients.Remove(source);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    sourcePatientID = request.SourcePatientID,
                    targetPatientID = request.TargetPatientID,
                    transferredStudies = studies.Count,
                    transferredImages = images.Count,
                    sourceNationalCode = source.NationalCode,
                    targetNationalCode = target.NationalCode,
                    message = "Patients merged successfully. Studies, images and Study-image links were preserved."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                RollBackMovedFiles(moved);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Patient merge failed. Database changes were rolled back and the system attempted to restore moved files.",
                    error = ex.Message
                });
            }
        }

        private void RollBackMovedFiles(List<(string OldRelativePath, string NewRelativePath)> moved)
        {
            for (int i = moved.Count - 1; i >= 0; i--)
            {
                try
                {
                    string oldPath = _storageService.GetPhysicalPath(moved[i].OldRelativePath);
                    string newPath = _storageService.GetPhysicalPath(moved[i].NewRelativePath);
                    if (System.IO.File.Exists(newPath) && !System.IO.File.Exists(oldPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(oldPath)!);
                        System.IO.File.Move(newPath, oldPath);
                        _storageService.DeletePatientFolderIfEmpty(Path.GetDirectoryName(moved[i].NewRelativePath));
                    }
                }
                catch { }
            }
        }
        // Saves/replaces the optional Patient profile photo.
        // capture="environment" on the frontend lets mobile devices open their camera.
        [HttpPost("{patientID:int}/photo")]
        public async Task<IActionResult> UploadPatientPhoto(int patientID, IFormFile file)
        {
            if (patientID <= 0) return BadRequest(new { success = false, message = "PatientID is invalid." });
            if (file == null || file.Length == 0 || string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "لطفاً یک فایل تصویری معتبر انتخاب کنید." });

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10) extension = ".jpg";
            var relativePath = Path.Combine("Patients", patientID.ToString(), "Profile", $"patient-{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
            var root = @"D:\RadiologyData";
            var fullPath = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            if (!string.IsNullOrWhiteSpace(patient.PhotoRelativePath))
            {
                var oldPath = Path.Combine(root, patient.PhotoRelativePath);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            patient.PhotoRelativePath = relativePath;
            patient.ModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, patient.PatientID, patient.PhotoRelativePath });
        }

        [HttpGet("{patientID:int}/photo")]
        public async Task<IActionResult> GetPatientPhoto(int patientID)
        {
            var path = await _context.Patients.AsNoTracking()
                .Where(p => p.PatientID == patientID)
                .Select(p => p.PhotoRelativePath)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(path)) return NotFound();
            var fullPath = Path.Combine(@"D:\RadiologyData", path);
            if (!System.IO.File.Exists(fullPath)) return NotFound();
            return PhysicalFile(fullPath, "image/jpeg");
        }

    }
}
