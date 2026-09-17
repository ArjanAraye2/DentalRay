using DentalRay.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // ============================================================
    // RadiologyStudiesDeleteController
    // ============================================================
    //
    // سیاست حذف Study در DentalRay:
    //
    // - اگر Study هیچ تصویری نداشته باشد، قابل حذف است.
    // - اگر حتی یک تصویر به Study متصل باشد، حذف مجاز نیست.
    // - کاربر باید ابتدا تصاویر Study را حذف کند.
    //
    // Route عمداً با RadiologyStudiesController یکسان است تا
    // Endpoint نهایی به شکل زیر باقی بماند:
    //
    // DELETE /api/radiologystudies/{studyID}
    // ============================================================
    [ApiController]
    [Route("api/radiologystudies")]
    public class RadiologyStudiesDeleteController : ControllerBase
    {
        private readonly DentalRayDbContext _context;

        public RadiologyStudiesDeleteController(
            DentalRayDbContext context)
        {
            _context = context;
        }

        // ========================================================
        // DELETE
        // حذف Study فقط در صورتی که هیچ Image نداشته باشد
        // ========================================================
        [HttpDelete("{studyID:int}")]
        public async Task<IActionResult> DeleteStudy(int studyID)
        {
            // StudyID باید معتبر باشد.
            if (studyID <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "StudyID must be greater than zero."
                });
            }

            // Study موردنظر را دریافت می‌کنیم.
            var study = await _context.RadiologyStudies
                .FirstOrDefaultAsync(s => s.StudyID == studyID);

            if (study == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Study not found."
                });
            }

            // فقط وجود حداقل یک Image برای جلوگیری از حذف کافی است.
            // AnyAsync از خواندن تمام رکوردهای Image جلوگیری می‌کند.
            bool hasImages = await _context.RadiologyImages
                .AsNoTracking()
                .AnyAsync(image => image.StudyID == studyID);

            if (hasImages)
            {
                return Conflict(new
                {
                    success = false,
                    message = "Study cannot be deleted because it has attached images. Delete the images first.",
                    studyID = studyID
                });
            }

            // هیچ Image وابسته‌ای وجود ندارد؛ حذف Study مجاز است.
            _context.RadiologyStudies.Remove(study);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                studyID = studyID,
                message = "Study deleted successfully."
            });
        }
    }
}
