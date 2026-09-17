using DentalRay.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // ============================================================
    // RadiologyStudiesController - Delete Study
    // ============================================================
    //
    // سیاست حذف Study در DentalRay:
    //
    // - اگر Study هیچ تصویری نداشته باشد، قابل حذف است.
    // - اگر حتی یک تصویر به Study متصل باشد، حذف مجاز نیست.
    // - در این حالت کاربر باید ابتدا تصاویر Study را حذف کند.
    //
    // این سیاست از حذف ناخواسته Study و از بین رفتن ارتباط
    // تصاویر رادیولوژی جلوگیری می‌کند.
    // ============================================================
    public partial class RadiologyStudiesController
    {
        // ========================================================
        // DELETE
        // حذف Study فقط در صورتی که هیچ Image نداشته باشد
        // ========================================================
        //
        // مثال:
        // DELETE /api/radiologystudies/7
        //
        // پاسخ‌های اصلی:
        //
        // 200 OK
        // Study با موفقیت حذف شد.
        //
        // 404 Not Found
        // Study وجود ندارد.
        //
        // 409 Conflict
        // Study دارای تصویر است و قابل حذف نیست.
        // ========================================================
        [HttpDelete("{studyID:int}")]
        public async Task<IActionResult> DeleteStudy(int studyID)
        {
            // ----------------------------------------------------
            // بررسی StudyID
            // ----------------------------------------------------
            if (studyID <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "StudyID must be greater than zero."
                });
            }

            // ----------------------------------------------------
            // دریافت Study
            // ----------------------------------------------------
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

            // ----------------------------------------------------
            // بررسی وجود Image وابسته
            // ----------------------------------------------------
            //
            // از AnyAsync استفاده می‌کنیم چون برای تصمیم حذف،
            // فقط دانستن وجود حداقل یک Image کافی است و نیازی
            // به خواندن تمام رکوردهای Image نداریم.
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

            // ----------------------------------------------------
            // حذف Study
            // ----------------------------------------------------
            //
            // در این نقطه مطمئن هستیم هیچ Image وابسته‌ای وجود
            // ندارد؛ بنابراین حذف Study با سیاست DentalRay سازگار است.
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
