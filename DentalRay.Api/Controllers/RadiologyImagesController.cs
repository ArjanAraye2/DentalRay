using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RadiologyImagesController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly RadiologyStorageService _storage;
        private readonly StudyAccessService _studyAccess;

        public RadiologyImagesController(DentalRayDbContext context, RadiologyStorageService storage, StudyAccessService studyAccess)
        {
            _context = context;
            _storage = storage;
            _studyAccess = studyAccess;
        }

        // New files can only enter DentalRay through a Study that the current user may access.
        [HttpPost]
        public async Task<IActionResult> UploadImage(int studyID, int imageTypeID, IFormFile file)
        {
            if (studyID <= 0 || file == null || file.Length == 0)
                return BadRequest(new { success=false, message="Study and file are required." });
            if (!await _studyAccess.CanAccessStudyAsync(studyID,User))
                return NotFound(new { success=false,message="Study not found." });
            if (imageTypeID <= 0)
                return BadRequest(new { success=false, message="نوع تصویر را انتخاب کنید.", messageEn="Image Type is required." });
            if (!await _context.ImageTypes.AsNoTracking().AnyAsync(x=>x.ImageTypeID==imageTypeID && x.IsActive))
                return BadRequest(new { success=false, message="نوع تصویر انتخاب‌شده معتبر یا فعال نیست.", messageEn="The selected Image Type is invalid or inactive." });


            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            // DentalRay accepts any browser-supplied image format, plus PDF.
            // Image Type (OPG, CBCT, ...) is a separate clinical lookup and is not a file-format restriction.
            bool isImage = !string.IsNullOrWhiteSpace(file.ContentType) &&
                           file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            bool isPdf = string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
                         ext == ".pdf";
            if (!isImage && !isPdf)
                return BadRequest(new
                {
                    success = false,
                    message = "فایل انتخاب‌شده باید تصویر یا PDF باشد.",
                    messageEn = "The selected file must be an image or PDF."
                });
            if (!await HasValidSignatureAsync(file, ext))
                return BadRequest(new { success=false, message="The selected file signature is invalid." });

            var study = await _context.RadiologyStudies.AsNoTracking().FirstAsync(x=>x.StudyID==studyID);
            var patient = await _context.Patients.AsNoTracking().FirstOrDefaultAsync(x=>x.PatientID==study.PatientID);
            if (patient == null) return NotFound(new { success=false, message="Patient not found." });

            int serial = (await _context.RadiologyImages.Where(x=>x.PatientID==patient.PatientID)
                .Select(x=>(int?)x.SerialNumber).MaxAsync() ?? 0) + 1;
            DateTime now = DateTime.Now;
            string? relativePath = null;
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await using (var input=file.OpenReadStream())
                    relativePath = await _storage.SaveImageAsync(input,file.FileName,patient.NationalCode,now,serial);
                var image = new RadiologyImage { PatientID=patient.PatientID,ImageTypeID=imageTypeID,FileName=Path.GetFileName(relativePath),RelativePath=relativePath,ContentType=ContentTypeFor(file, ext),SerialNumber=serial,CreatedDate=now };
                _context.RadiologyImages.Add(image); await _context.SaveChangesAsync();
                _context.RadiologyStudyImages.Add(new RadiologyStudyImage { StudyID=studyID,ImageID=image.ImageID,CreatedDate=now });
                await _context.SaveChangesAsync(); await tx.CommitAsync();
                return Ok(new { success=true,imageID=image.ImageID,patientID=image.PatientID,studyID,image.ImageTypeID,image.FileName,image.RelativePath,image.ContentType,image.SerialNumber,image.CreatedDate });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                if (!string.IsNullOrWhiteSpace(relativePath)) try { _storage.DeleteFile(relativePath); } catch { }
                return StatusCode(500,new { success=false,message="Image upload failed.",error=ex.Message });
            }
        }

        [HttpGet("{imageID:long}")]
        public async Task<IActionResult> GetImage(long imageID)
        {
            var image=await _context.RadiologyImages.AsNoTracking().FirstOrDefaultAsync(x=>x.ImageID==imageID);
            if (image==null || !await CanAccessImageAsync(imageID)) return NotFound(new { success=false,message="Image not found." });
            string path=_storage.GetPhysicalPath(image.RelativePath);
            if (!System.IO.File.Exists(path)) return NotFound(new { success=false,message="Physical file not found." });
            return PhysicalFile(path,image.ContentType,image.FileName,enableRangeProcessing:true);
        }

        [HttpGet("study/{studyID:int}")]
        public async Task<IActionResult> GetStudyImages(int studyID)
        {
            if (!await _studyAccess.CanAccessStudyAsync(studyID,User)) return NotFound(new { success=false,message="Study not found." });
            var images=await (from l in _context.RadiologyStudyImages.AsNoTracking() join i in _context.RadiologyImages.AsNoTracking() on l.ImageID equals i.ImageID join t in _context.ImageTypes.AsNoTracking() on i.ImageTypeID equals t.ImageTypeID into types from t in types.DefaultIfEmpty() where l.StudyID==studyID orderby i.FileName descending select new { i.ImageID,i.PatientID,i.ImageTypeID,ImageTypeName=t!=null?t.ImageTypeName:null,i.FileName,i.RelativePath,i.ContentType,i.SerialNumber,i.CreatedDate }).ToListAsync();
            return Ok(new { success=true,studyID,count=images.Count,images });
        }

        // Only images reachable through at least one accessible Study are returned to ordinary users.
        [HttpGet("patient/{patientID:int}")]
        public async Task<IActionResult> GetPatientImages(int patientID)
        {
            if (!await _context.Patients.AnyAsync(x=>x.PatientID==patientID)) return NotFound(new { success=false,message="Patient not found." });
            var accessibleStudyIDs=_studyAccess.ApplyAccess(_context.RadiologyStudies.AsNoTracking().Where(s=>s.PatientID==patientID),User).Select(s=>s.StudyID);
            var accessibleImageIDs=_context.RadiologyStudyImages.AsNoTracking().Where(l=>accessibleStudyIDs.Contains(l.StudyID)).Select(l=>l.ImageID).Distinct();
            var images=await _context.RadiologyImages.AsNoTracking().Where(x=>x.PatientID==patientID && (StudyAccessService.IsSuperAdmin(User) || accessibleImageIDs.Contains(x.ImageID))).OrderByDescending(x=>x.FileName)
                .Select(x=>new { x.ImageID,x.ImageTypeID,ImageTypeName=_context.ImageTypes.Where(t=>t.ImageTypeID==x.ImageTypeID).Select(t=>t.ImageTypeName).FirstOrDefault(),x.FileName,x.ContentType,x.CreatedDate,studyCount=_context.RadiologyStudyImages.Count(l=>l.ImageID==x.ImageID) }).ToListAsync();
            return Ok(new { success=true,patientID,count=images.Count,images });
        }

        [HttpGet("study/{studyID:int}/picker")]
        public async Task<IActionResult> GetPicker(int studyID)
        {
            if (!await _studyAccess.CanAccessStudyAsync(studyID,User)) return NotFound(new { success=false,message="Study not found." });
            var study=await _context.RadiologyStudies.AsNoTracking().FirstAsync(x=>x.StudyID==studyID);
            var attached=await _context.RadiologyStudyImages.AsNoTracking().Where(x=>x.StudyID==studyID).Select(x=>x.ImageID).ToListAsync();
            var accessibleStudyIDs=_studyAccess.ApplyAccess(_context.RadiologyStudies.AsNoTracking().Where(s=>s.PatientID==study.PatientID),User).Select(s=>s.StudyID);
            var accessibleImageIDs=_context.RadiologyStudyImages.AsNoTracking().Where(l=>accessibleStudyIDs.Contains(l.StudyID)).Select(l=>l.ImageID).Distinct();
            var images=await _context.RadiologyImages.AsNoTracking().Where(x=>x.PatientID==study.PatientID && (StudyAccessService.IsSuperAdmin(User) || accessibleImageIDs.Contains(x.ImageID)))
                .OrderByDescending(x=>x.FileName).Select(x=>new { x.ImageID,x.ImageTypeID,ImageTypeName=_context.ImageTypes.Where(t=>t.ImageTypeID==x.ImageTypeID).Select(t=>t.ImageTypeName).FirstOrDefault(),x.FileName,x.ContentType,attached=attached.Contains(x.ImageID) }).ToListAsync();
            return Ok(new { success=true,studyID,images });
        }

        [HttpPost("study/{studyID:int}/attach")]
        public async Task<IActionResult> Attach(int studyID,[FromBody] long[] imageIDs)
        {
            if (!await _studyAccess.CanAccessStudyAsync(studyID,User)) return NotFound(new { success=false,message="Study not found." });
            var study=await _context.RadiologyStudies.FirstAsync(x=>x.StudyID==studyID);
            var ids=(imageIDs??Array.Empty<long>()).Where(x=>x>0).Distinct().ToList();
            if (ids.Count==0) return BadRequest(new { success=false,message="At least one ImageID is required." });
            foreach(long id in ids) if (!await CanAccessImageAsync(id)) return NotFound(new { success=false,message="One or more images do not exist." });
            var images=await _context.RadiologyImages.Where(x=>ids.Contains(x.ImageID)).ToListAsync();
            if (images.Count!=ids.Count) return BadRequest(new { success=false,message="One or more images do not exist." });
            if (images.Any(x=>x.PatientID!=study.PatientID)) return Conflict(new { success=false,message="An image cannot be attached to another patient's Study." });
            var existing=await _context.RadiologyStudyImages.Where(x=>x.StudyID==studyID && ids.Contains(x.ImageID)).Select(x=>x.ImageID).ToListAsync();
            foreach(long id in ids.Except(existing)) _context.RadiologyStudyImages.Add(new RadiologyStudyImage { StudyID=studyID,ImageID=id,CreatedDate=DateTime.Now });
            await _context.SaveChangesAsync();
            return Ok(new { success=true,attached=ids.Count-existing.Count,alreadyAttached=existing.Count });
        }

        [HttpPost("study/{studyID:int}/detach")]
        public async Task<IActionResult> Detach(int studyID,[FromBody] long[] imageIDs)
        {
            if (!await _studyAccess.CanAccessStudyAsync(studyID,User)) return NotFound(new { success=false,message="Study not found." });
            var ids=(imageIDs??Array.Empty<long>()).Distinct().ToList();
            var links=await _context.RadiologyStudyImages.Where(x=>x.StudyID==studyID && ids.Contains(x.ImageID)).ToListAsync();
            _context.RadiologyStudyImages.RemoveRange(links); await _context.SaveChangesAsync();
            var stillLinked=await _context.RadiologyStudyImages.Where(x=>ids.Contains(x.ImageID)).Select(x=>x.ImageID).Distinct().ToListAsync();
            return Ok(new { success=true,detached=links.Count,unattachedImageIDs=ids.Except(stillLinked).ToArray() });
        }

        // An unattached image has no Study scope. To avoid privilege escalation, only Super Admin may permanently delete it.
        [HttpDelete("{imageID:long}")]
        public async Task<IActionResult> DeleteImage(long imageID)
        {
            if (!StudyAccessService.IsSuperAdmin(User)) return Forbid();
            var image=await _context.RadiologyImages.FirstOrDefaultAsync(x=>x.ImageID==imageID);
            if (image==null) return NotFound(new { success=false,message="Image not found." });
            if (await _context.RadiologyStudyImages.AnyAsync(x=>x.ImageID==imageID)) return Conflict(new { success=false,message="Detach the image from all Studies before deleting it." });
            string originalPath=_storage.GetPhysicalPath(image.RelativePath);
            if (!System.IO.File.Exists(originalPath)) return Conflict(new { success=false,message="Physical file not found. Database metadata was not deleted." });
            string directory=Path.GetDirectoryName(originalPath)!;
            string temporaryPath=Path.Combine(directory,$".delete_{Guid.NewGuid():N}_{Path.GetFileName(originalPath)}");
            bool quarantined=false;
            await using var tx=await _context.Database.BeginTransactionAsync();
            try
            {
                System.IO.File.Move(originalPath,temporaryPath); quarantined=true; _context.RadiologyImages.Remove(image); await _context.SaveChangesAsync(); await tx.CommitAsync();
            }
            catch(Exception ex)
            {
                try { await tx.RollbackAsync(); } catch { }
                try { if (quarantined && System.IO.File.Exists(temporaryPath) && !System.IO.File.Exists(originalPath)) System.IO.File.Move(temporaryPath,originalPath); } catch { }
                return StatusCode(500,new { success=false,message="Image delete failed. Database changes were rolled back and the system attempted to restore the physical file.",error=ex.Message });
            }
            bool cleanupWarning=false; string? cleanupMessage=null;
            try { if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath); _storage.DeletePatientFolderIfEmpty(Path.GetDirectoryName(image.RelativePath)); }
            catch(Exception ex) { cleanupWarning=true; cleanupMessage=ex.Message; }
            return Ok(new { success=true,imageID,cleanupWarning,cleanupMessage,message=cleanupWarning ? "Image metadata was deleted, but temporary-file cleanup could not be completed." : "Image deleted successfully." });
        }

        private async Task<bool> CanAccessImageAsync(long imageID)
        {
            if (StudyAccessService.IsSuperAdmin(User)) return await _context.RadiologyImages.AsNoTracking().AnyAsync(i=>i.ImageID==imageID);
            var studyIDs=_studyAccess.ApplyAccess(_context.RadiologyStudies.AsNoTracking(),User).Select(s=>s.StudyID);
            return await _context.RadiologyStudyImages.AsNoTracking().AnyAsync(l=>l.ImageID==imageID && studyIDs.Contains(l.StudyID));
        }

        private static string ContentTypeFor(IFormFile file, string ext)
        {
            if (ext == ".pdf") return "application/pdf";
            return !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream";
        }
        private static async Task<bool> HasValidSignatureAsync(IFormFile file,string ext)
        {
            byte[] b=new byte[8]; await using var s=file.OpenReadStream(); int n=await s.ReadAsync(b.AsMemory(0,b.Length));
            if (ext is ".jpg" or ".jpeg") return n>=3 && b[0]==0xFF && b[1]==0xD8 && b[2]==0xFF;
            if (ext==".png") return n>=8 && b.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
            if (ext==".pdf") return n>=5 && b[0]==0x25 && b[1]==0x50 && b[2]==0x44 && b[3]==0x46 && b[4]==0x2D;
            // For other image formats, the browser-provided image/* MIME type was already checked.
            // Do not reject them merely because DentalRay has no format-specific signature rule yet.
            return !string.IsNullOrWhiteSpace(file.ContentType) &&
                   file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }
    }
}