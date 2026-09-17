using System.Text.RegularExpressions;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // Patient operations. Images are owned by Patient; Study links are stored separately.
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly RadiologyStorageService _storageService;

        public PatientsController(DentalRayDbContext context, RadiologyStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPatients(string? search = null, bool includeInactive = false)
        {
            var query = _context.Patients.AsNoTracking().AsQueryable();
            if (!includeInactive) query = query.Where(p => p.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string text = search.Trim();
                query = query.Where(p => p.NationalCode.Contains(text) || p.FirstName.Contains(text) ||
                    p.LastName.Contains(text) || (p.Mobile != null && p.Mobile.Contains(text)));
            }

            var patients = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Take(100)
                .Select(p => new { p.PatientID, p.NationalCode, p.FirstName, p.LastName, p.BirthDate,
                    p.Gender, p.Mobile, p.IsActive, p.CreatedDate, p.ModifiedDate }).ToListAsync();
            return Ok(new { success = true, count = patients.Count, patients });
        }

        [HttpGet("{nationalCode}")]
        public async Task<IActionResult> GetPatient(string nationalCode)
        {
            var patient = await _context.Patients.AsNoTracking()
                .FirstOrDefaultAsync(p => p.NationalCode == nationalCode);
            return patient == null ? NotFound(new { success = false, message = "Patient not found." }) : Ok(patient);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePatient(Patient patient)
        {
            patient.NationalCode = patient.NationalCode.Trim();
            if (!Regex.IsMatch(patient.NationalCode, @"^\d+$"))
                return BadRequest(new { success = false, message = "NationalCode must contain only digits." });
            if (await _context.Patients.AnyAsync(p => p.NationalCode == patient.NationalCode))
                return Conflict(new { success = false, message = "A patient with this NationalCode already exists." });

            patient.PatientID = 0;
            patient.CreatedDate = DateTime.Now;
            patient.ModifiedDate = null;
            patient.IsActive = true;
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            // Keep the create response consistent with the rest of the API. The frontend uses
            // this flag to distinguish a successful save from an API validation/conflict error.
            return Ok(new { success = true, patient });
        }

        // NationalCode correction also moves every Patient-owned image to the new folder/name.
        [HttpPut("{patientID:int}")]
        public async Task<IActionResult> UpdatePatient(int patientID, UpdatePatientRequest request)
        {
            if (patientID <= 0) return BadRequest(new { success = false, message = "PatientID must be greater than zero." });
            string newCode = request.NationalCode.Trim();
            if (!Regex.IsMatch(newCode, @"^\d+$"))
                return BadRequest(new { success = false, message = "NationalCode must contain only digits." });

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });
            bool codeChanged = !string.Equals(patient.NationalCode, newCode, StringComparison.Ordinal);
            if (codeChanged && await _context.Patients.AnyAsync(p => p.PatientID != patientID && p.NationalCode == newCode))
                return Conflict(new { success = false, message = "The new NationalCode already belongs to another patient. Use the Merge operation if these records represent the same patient." });

            var moved = new List<(string OldRelativePath, string NewRelativePath)>();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (codeChanged)
                {
                    var images = await _context.RadiologyImages.Where(i => i.PatientID == patientID)
                        .OrderBy(i => i.SerialNumber).ToListAsync();
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
                return StatusCode(500, new { success = false, message = "Patient update failed. Database changes were rolled back and the system attempted to restore moved image files.", error = ex.Message });
            }
        }

        [HttpPut("{patientID:int}/deactivate")]
        public async Task<IActionResult> DeactivatePatient(int patientID)
        {
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });
            if (!patient.IsActive) return Ok(new { success = true, message = "Patient is already inactive.", patientID });
            patient.IsActive = false; patient.ModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, patient.PatientID, patient.NationalCode, patient.IsActive, patient.ModifiedDate });
        }

        [HttpPut("{patientID:int}/activate")]
        public async Task<IActionResult> ActivatePatient(int patientID)
        {
            if (patientID <= 0) return BadRequest(new { success = false, message = "PatientID must be greater than zero." });
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });
            if (!patient.IsActive) { patient.IsActive = true; patient.ModifiedDate = DateTime.Now; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, patient.PatientID, patient.NationalCode, patient.IsActive, patient.ModifiedDate, message = "Patient activated successfully." });
        }

        [HttpGet("{patientID:int}/details")]
        public async Task<IActionResult> GetPatientDetails(int patientID)
        {
            if (patientID <= 0) return BadRequest(new { success = false, message = "PatientID must be greater than zero." });
            var patient = await _context.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.PatientID == patientID);
            if (patient == null) return NotFound(new { success = false, message = "Patient not found." });

            var studies = await _context.RadiologyStudies.AsNoTracking().Where(s => s.PatientID == patientID)
                .OrderByDescending(s => s.StudyDate).ThenByDescending(s => s.StudyID).ToListAsync();
            var studyIDs = studies.Select(s => s.StudyID).ToList();
            var imageCounts = studyIDs.Count == 0 ? new Dictionary<int, int>() :
                await _context.RadiologyStudyImages.AsNoTracking().Where(x => studyIDs.Contains(x.StudyID))
                    .GroupBy(x => x.StudyID).Select(g => new { StudyID = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.StudyID, x => x.Count);
            int totalImageCount = await _context.RadiologyImages.AsNoTracking().CountAsync(i => i.PatientID == patientID);

            var studyList = studies.Select(s => new { s.StudyID, s.PatientID, s.StudyDate, s.StudyType, s.BodyPart,
                s.Description, s.Report, s.CreatedDate, s.ModifiedDate,
                imageCount = imageCounts.TryGetValue(s.StudyID, out int count) ? count : 0 }).ToList();
            return Ok(new { success = true, patient = new { patient.PatientID, patient.NationalCode, patient.FirstName,
                patient.LastName, patient.BirthDate, patient.Gender, patient.Mobile, patient.Address, patient.Description,
                patient.CreatedDate, patient.ModifiedDate, patient.IsActive }, studyCount = studyList.Count,
                totalImageCount, studies = studyList });
        }

        // Merge keeps all Studies and all Images. Source image serials are reassigned only when necessary
        // so the target Patient keeps a unique (PatientID, SerialNumber) sequence.
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
            var images = await _context.RadiologyImages.Where(i => i.PatientID == source.PatientID)
                .OrderBy(i => i.SerialNumber).ToListAsync();
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
                foreach (var study in studies) { study.PatientID = target.PatientID; study.ModifiedDate = DateTime.Now; }
                _context.Patients.Remove(source);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true, sourcePatientID = request.SourcePatientID, targetPatientID = request.TargetPatientID,
                    transferredStudies = studies.Count, transferredImages = images.Count,
                    sourceNationalCode = source.NationalCode, targetNationalCode = target.NationalCode,
                    message = "Patients merged successfully. Studies, images and Study-image links were preserved." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                RollBackMovedFiles(moved);
                return StatusCode(500, new { success = false, message = "Patient merge failed. Database changes were rolled back and the system attempted to restore moved files.", error = ex.Message });
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
                catch { /* Preserve the original exception; physical rollback is best effort. */ }
            }
        }
    }
}
