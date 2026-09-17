using DentalRay.Api.Data;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/radiologystudies")]
    public class RadiologyStudiesDeleteController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly RadiologyStorageService _storage;
        public RadiologyStudiesDeleteController(DentalRayDbContext context,RadiologyStorageService storage)
        { _context=context; _storage=storage; }

        // Preview lets Frontend show shared images separately from images used only by this Study.
        [HttpGet("{studyID:int}/delete-preview")]
        public async Task<IActionResult> Preview(int studyID)
        {
            if (!await _context.RadiologyStudies.AnyAsync(x=>x.StudyID==studyID)) return NotFound();
            var rows=await (from l in _context.RadiologyStudyImages
                            join i in _context.RadiologyImages on l.ImageID equals i.ImageID
                            where l.StudyID==studyID
                            select new { i.ImageID,i.FileName,
                                studyCount=_context.RadiologyStudyImages.Count(x=>x.ImageID==i.ImageID) }).ToListAsync();
            return Ok(new { success=true,studyID,
                sharedImages=rows.Where(x=>x.studyCount>1),
                studyOnlyImages=rows.Where(x=>x.studyCount==1) });
        }

        // deleteImageIDs may contain only Study-only images selected by the user.
        [HttpDelete("{studyID:int}")]
        public async Task<IActionResult> DeleteStudy(int studyID,[FromQuery] long[]? deleteImageIDs)
        {
            var study=await _context.RadiologyStudies.FirstOrDefaultAsync(x=>x.StudyID==studyID);
            if (study==null) return NotFound(new { success=false,message="Study not found." });
            var selected=(deleteImageIDs??Array.Empty<long>()).Distinct().ToHashSet();
            var links=await _context.RadiologyStudyImages.Where(x=>x.StudyID==studyID).ToListAsync();
            var ids=links.Select(x=>x.ImageID).ToList();
            var shared=await _context.RadiologyStudyImages.Where(x=>ids.Contains(x.ImageID) && x.StudyID!=studyID)
                .Select(x=>x.ImageID).Distinct().ToListAsync();
            if (selected.Overlaps(shared)) return Conflict(new { success=false,message="Shared images cannot be deleted with this Study." });
            if (selected.Any(x=>!ids.Contains(x))) return BadRequest(new { success=false,message="A selected image is not attached to this Study." });

            var deleteImages=await _context.RadiologyImages.Where(x=>selected.Contains(x.ImageID)).ToListAsync();
            var paths=deleteImages.Select(x=>x.RelativePath).ToList();
            await using var tx=await _context.Database.BeginTransactionAsync();
            try
            {
                _context.RadiologyStudyImages.RemoveRange(links);
                _context.RadiologyImages.RemoveRange(deleteImages);
                _context.RadiologyStudies.Remove(study);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch { await tx.RollbackAsync(); throw; }

            // Database is authoritative; physical cleanup occurs after successful commit.
            var cleanupErrors=new List<string>();
            foreach(string path in paths) try { _storage.DeleteFile(path); } catch(Exception ex) { cleanupErrors.Add(ex.Message); }
            return Ok(new { success=true,studyID,deletedImages=deleteImages.Count,
                retainedImages=ids.Count-deleteImages.Count,cleanupErrors });
        }
    }
}
