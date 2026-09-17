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

        public RadiologyImagesController(DentalRayDbContext context, RadiologyStorageService storage)
        {
            _context = context;
            _storage = storage;
        }

        // New files can only enter DentalRay through a Study. One file per request.
        [HttpPost]
        public async Task<IActionResult> UploadImage(int studyID, IFormFile file)
        {
            if (studyID <= 0 || file == null || file.Length == 0)
                return BadRequest(new { success=false, message="Study and file are required." });

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".pdf"))
                return BadRequest(new { success=false, message="Only JPG, JPEG, PNG and PDF files are allowed." });

            if (!await HasValidSignatureAsync(file, ext))
                return BadRequest(new { success=false, message="The selected file signature is invalid." });

            var study = await _context.RadiologyStudies.AsNoTracking().FirstOrDefaultAsync(x=>x.StudyID==studyID);
            if (study == null) return NotFound(new { success=false, message="Study not found." });
            var patient = await _context.Patients.AsNoTracking().FirstOrDefaultAsync(x=>x.PatientID==study.PatientID);
            if (patient == null) return NotFound(new { success=false, message="Patient not found." });

            int serial = (await _context.RadiologyImages
                .Where(x=>x.PatientID==patient.PatientID)
                .Select(x=>(int?)x.SerialNumber).MaxAsync() ?? 0) + 1;

            DateTime now = DateTime.Now;
            string? relativePath = null;
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await using (var input=file.OpenReadStream())
                    relativePath = await _storage.SaveImageAsync(input,file.FileName,patient.NationalCode,now,serial);

                var image = new RadiologyImage
                {
                    PatientID=patient.PatientID,
                    FileName=Path.GetFileName(relativePath),
                    RelativePath=relativePath,
                    ContentType=ContentTypeFor(ext),
                    SerialNumber=serial,
                    CreatedDate=now
                };
                _context.RadiologyImages.Add(image);
                await _context.SaveChangesAsync();

                _context.RadiologyStudyImages.Add(new RadiologyStudyImage
                {
                    StudyID=studyID, ImageID=image.ImageID, CreatedDate=now
                });
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new { success=true, imageID=image.ImageID, patientID=image.PatientID,
                    studyID, image.FileName, image.RelativePath, image.ContentType,
                    image.SerialNumber, image.CreatedDate });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    try { _storage.DeleteFile(relativePath); } catch { }
                }
                return StatusCode(500,new { success=false, message="Image upload failed.", error=ex.Message });
            }
        }

        [HttpGet("{imageID:long}")]
        public async Task<IActionResult> GetImage(long imageID)
        {
            var image=await _context.RadiologyImages.AsNoTracking().FirstOrDefaultAsync(x=>x.ImageID==imageID);
            if (image==null) return NotFound(new { success=false,message="Image not found." });
            string path=_storage.GetPhysicalPath(image.RelativePath);
            if (!System.IO.File.Exists(path)) return NotFound(new { success=false,message="Physical file not found." });
            return PhysicalFile(path,image.ContentType,image.FileName,enableRangeProcessing:true);
        }

        // Gallery for a Study, filename descending as approved.
        [HttpGet("study/{studyID:int}")]
        public async Task<IActionResult> GetStudyImages(int studyID)
        {
            if (!await _context.RadiologyStudies.AnyAsync(x=>x.StudyID==studyID))
                return NotFound(new { success=false,message="Study not found." });

            var images=await (from l in _context.RadiologyStudyImages.AsNoTracking()
                              join i in _context.RadiologyImages.AsNoTracking() on l.ImageID equals i.ImageID
                              where l.StudyID==studyID
                              orderby i.FileName descending
                              select new { i.ImageID,i.PatientID,i.FileName,i.RelativePath,i.ContentType,i.SerialNumber,i.CreatedDate })
                              .ToListAsync();
            return Ok(new { success=true,studyID,count=images.Count,images });
        }

        // Patient Images section. Frontend hides the section when count is zero.
        [HttpGet("patient/{patientID:int}")]
        public async Task<IActionResult> GetPatientImages(int patientID)
        {
            if (!await _context.Patients.AnyAsync(x=>x.PatientID==patientID))
                return NotFound(new { success=false,message="Patient not found." });

            var images=await _context.RadiologyImages.AsNoTracking()
                .Where(x=>x.PatientID==patientID).OrderByDescending(x=>x.FileName)
                .Select(x=>new { x.ImageID,x.FileName,x.ContentType,x.CreatedDate,
                    studyCount=_context.RadiologyStudyImages.Count(l=>l.ImageID==x.ImageID) })
                .ToListAsync();
            return Ok(new { success=true,patientID,count=images.Count,images });
        }

        // Picker shows all Patient images and tells Frontend which are already attached.
        [HttpGet("study/{studyID:int}/picker")]
        public async Task<IActionResult> GetPicker(int studyID)
        {
            var study=await _context.RadiologyStudies.AsNoTracking().FirstOrDefaultAsync(x=>x.StudyID==studyID);
            if (study==null) return NotFound(new { success=false,message="Study not found." });
            var attached=await _context.RadiologyStudyImages.AsNoTracking().Where(x=>x.StudyID==studyID).Select(x=>x.ImageID).ToListAsync();
            var images=await _context.RadiologyImages.AsNoTracking().Where(x=>x.PatientID==study.PatientID)
                .OrderByDescending(x=>x.FileName)
                .Select(x=>new { x.ImageID,x.FileName,x.ContentType,attached=attached.Contains(x.ImageID) }).ToListAsync();
            return Ok(new { success=true,studyID,images });
        }

        // Supports one or several existing image IDs; backend enforces same Patient.
        [HttpPost("study/{studyID:int}/attach")]
        public async Task<IActionResult> Attach(int studyID,[FromBody] long[] imageIDs)
        {
            var study=await _context.RadiologyStudies.FirstOrDefaultAsync(x=>x.StudyID==studyID);
            if (study==null) return NotFound(new { success=false,message="Study not found." });
            var ids=(imageIDs??Array.Empty<long>()).Where(x=>x>0).Distinct().ToList();
            if (ids.Count==0) return BadRequest(new { success=false,message="At least one ImageID is required." });
            var images=await _context.RadiologyImages.Where(x=>ids.Contains(x.ImageID)).ToListAsync();
            if (images.Count!=ids.Count) return BadRequest(new { success=false,message="One or more images do not exist." });
            if (images.Any(x=>x.PatientID!=study.PatientID)) return Conflict(new { success=false,message="An image cannot be attached to another patient's Study." });
            var existing=await _context.RadiologyStudyImages.Where(x=>x.StudyID==studyID && ids.Contains(x.ImageID)).Select(x=>x.ImageID).ToListAsync();
            foreach(long id in ids.Except(existing)) _context.RadiologyStudyImages.Add(new RadiologyStudyImage { StudyID=studyID,ImageID=id,CreatedDate=DateTime.Now });
            await _context.SaveChangesAsync();
            return Ok(new { success=true,attached=ids.Count-existing.Count,alreadyAttached=existing.Count });
        }

        // Detach only removes links. Response identifies newly-unattached images so UI can immediately ask about deletion.
        [HttpPost("study/{studyID:int}/detach")]
        public async Task<IActionResult> Detach(int studyID,[FromBody] long[] imageIDs)
        {
            var ids=(imageIDs??Array.Empty<long>()).Distinct().ToList();
            var links=await _context.RadiologyStudyImages.Where(x=>x.StudyID==studyID && ids.Contains(x.ImageID)).ToListAsync();
            _context.RadiologyStudyImages.RemoveRange(links);
            await _context.SaveChangesAsync();
            var stillLinked=await _context.RadiologyStudyImages.Where(x=>ids.Contains(x.ImageID)).Select(x=>x.ImageID).Distinct().ToListAsync();
            var unattached=ids.Except(stillLinked).ToArray();
            return Ok(new { success=true,detached=links.Count,unattachedImageIDs=unattached });
        }

        // Physical deletion is allowed only when no Study link remains.
        [HttpDelete("{imageID:long}")]
        public async Task<IActionResult> DeleteImage(long imageID)
        {
            var image=await _context.RadiologyImages.FirstOrDefaultAsync(x=>x.ImageID==imageID);
            if (image==null) return NotFound(new { success=false,message="Image not found." });
            if (await _context.RadiologyStudyImages.AnyAsync(x=>x.ImageID==imageID))
                return Conflict(new { success=false,message="Detach the image from all Studies before deleting it." });

            string path=image.RelativePath;
            _context.RadiologyImages.Remove(image);
            await _context.SaveChangesAsync();
            try { _storage.DeleteFile(path); }
            catch (Exception ex) { return StatusCode(500,new { success=false,message="Database record was deleted but physical-file cleanup failed.",error=ex.Message }); }
            return Ok(new { success=true,imageID });
        }

        private static string ContentTypeFor(string ext)=>ext switch
        { ".png"=>"image/png", ".pdf"=>"application/pdf", _=>"image/jpeg" };

        private static async Task<bool> HasValidSignatureAsync(IFormFile file,string ext)
        {
            byte[] b=new byte[8];
            await using var s=file.OpenReadStream();
            int n=await s.ReadAsync(b.AsMemory(0,b.Length));
            if (ext is ".jpg" or ".jpeg") return n>=3 && b[0]==0xFF && b[1]==0xD8 && b[2]==0xFF;
            if (ext==".png") return n>=8 && b.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
            if (ext==".pdf") return n>=5 && b[0]==0x25 && b[1]==0x50 && b[2]==0x44 && b[3]==0x46 && b[4]==0x2D;
            return false;
        }
    }
}
