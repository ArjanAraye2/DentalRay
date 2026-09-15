using System.Text.RegularExpressions;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // ============================================================
    // RadiologyImagesController
    // ============================================================
    //
    // مسئول عملیات تصاویر رادیولوژی است.
    //
    // قابلیت‌های این Controller:
    //
    // - Upload تصویر
    // - دریافت فایل تصویر
    // - دریافت لیست تصاویر یک Study
    // - حذف تصویر
    //
    // نکته مهم:
    //
    // فایل واقعی تصویر روی Disk ذخیره می‌شود.
    //
    // SQL Server فقط Metadata را نگهداری می‌کند.
    //
    // بنابراین در عملیات Upload و Delete باید همیشه تلاش کنیم:
    //
    // Database
    //
    // و
    //
    // Physical File
    //
    // با یکدیگر هماهنگ باقی بمانند.
    //
    // ============================================================

    [ApiController]
    [Route("api/[controller]")]
    public class RadiologyImagesController :
        ControllerBase
    {
        private readonly DentalRayDbContext
            _context;


        private readonly RadiologyStorageService
            _storageService;


        // ========================================================
        // تنظیمات Upload
        // ========================================================

        // حداکثر حجم هر تصویر:
        //
        // 25 MB
        //
        private const long MaxImageSize =
            25L * 1024L * 1024L;


        // ========================================================
        // Constructor
        // ========================================================

        public RadiologyImagesController(
            DentalRayDbContext context,
            RadiologyStorageService storageService)
        {
            _context =
                context;


            _storageService =
                storageService;
        }


        // ========================================================
        // POST
        // Upload تصویر
        // ========================================================
        //
        // مثال:
        //
        // POST /api/radiologyimages?studyID=7
        //
        // فایل به صورت multipart/form-data ارسال می‌شود.
        //
        // ========================================================

        [HttpPost]
        public async Task<IActionResult> UploadImage(
            int studyID,
            IFormFile file,
            [FromForm] string? description = null)
        {
            try
            {
                // ------------------------------------------------
                // بررسی StudyID
                // ------------------------------------------------

                if (studyID <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "StudyID must be greater than zero."
                    });
                }


                // ------------------------------------------------
                // بررسی وجود فایل
                // ------------------------------------------------

                if (file == null ||
                    file.Length == 0)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "Image file is required."
                    });
                }


                // ------------------------------------------------
                // توضیح تصویر
                // ------------------------------------------------
                description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                if (description != null && description.Length > 1000)
                    return BadRequest(new { success = false, message = "Image description cannot be longer than 1000 characters." });

                // ------------------------------------------------
                // محدودیت حجم
                // ------------------------------------------------

                if (file.Length >
                    MaxImageSize)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "Image file is too large. Maximum size is 25 MB."
                    });
                }


                // =================================================
                // بررسی پسوند فایل
                // =================================================

                string extension =
                    Path.GetExtension(
                        file.FileName)
                    .ToLowerInvariant();


                bool extensionIsValid =
                    extension == ".jpg" ||
                    extension == ".jpeg" ||
                    extension == ".png";


                if (!extensionIsValid)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "Only JPG, JPEG and PNG files are allowed."
                    });
                }


                // =================================================
                // بررسی Header واقعی فایل
                // =================================================
                //
                // فقط بررسی extension کافی نیست.
                //
                // ممکن است کاربر فایل دیگری را Rename کرده باشد:
                //
                // document.exe
                //
                // به:
                //
                // document.jpg
                //
                // بنابراین Signature واقعی فایل نیز بررسی می‌شود.
                //
                // =================================================

                bool signatureIsValid =
                    await HasValidImageSignatureAsync(
                        file,
                        extension);


                if (!signatureIsValid)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "The selected file is not a valid JPG or PNG image."
                    });
                }


                // ------------------------------------------------
                // دریافت Study
                // ------------------------------------------------

                var study =
                    await _context.RadiologyStudies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            s => s.StudyID ==
                                 studyID);


                if (study == null)
                {
                    return NotFound(new
                    {
                        success = false,

                        message =
                            "Study not found."
                    });
                }


                // ------------------------------------------------
                // دریافت Patient مربوط به Study
                // ------------------------------------------------

                var patient =
                    await _context.Patients
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            p => p.PatientID ==
                                 study.PatientID);


                if (patient == null)
                {
                    return NotFound(new
                    {
                        success = false,

                        message =
                            "Patient not found."
                    });
                }


                // =================================================
                // تعیین Serial بعدی
                // =================================================
                //
                // مثال:
                //
                // 1860271855_7_001.jpg
                // 1860271855_7_002.jpg
                //
                // تصویر بعدی:
                //
                // 1860271855_7_003.jpg
                //
                // =================================================

                var existingFileNames =
                    await _context
                        .RadiologyImages
                        .AsNoTracking()

                        .Where(
                            i => i.StudyID ==
                                 studyID)

                        .Select(
                            i => i.FileName)

                        .ToListAsync();


                int nextSerial = 1;


                foreach (string fileName
                         in existingFileNames)
                {
                    Match match =
                        Regex.Match(
                            fileName,

                            @"_(\d{3})\.(jpg|jpeg|png)$",

                            RegexOptions
                                .IgnoreCase);


                    if (!match.Success)
                    {
                        continue;
                    }


                    if (int.TryParse(
                        match.Groups[1].Value,
                        out int serial))
                    {
                        if (serial >=
                            nextSerial)
                        {
                            nextSerial =
                                serial + 1;
                        }
                    }
                }


                // ------------------------------------------------
                // Serial سه رقمی
                // ------------------------------------------------

                if (nextSerial > 999)
                {
                    return Conflict(new
                    {
                        success = false,

                        message =
                            "Maximum image serial 999 has been reached for this study."
                    });
                }


                // =================================================
                // ContentType معتبر
                // =================================================
                //
                // ContentType ارسالی Browser را مستقیماً اعتماد
                // نمی‌کنیم.
                //
                // آن را از extension تأییدشده تعیین می‌کنیم.
                //
                // =================================================

                string contentType =
                    extension == ".png"
                        ? "image/png"
                        : "image/jpeg";


                // =================================================
                // ذخیره فایل فیزیکی
                // =================================================

                string relativePath;


                using (Stream imageStream =
                       file.OpenReadStream())
                {
                    relativePath =
                        await _storageService
                            .SaveImageAsync(
                                imageStream,

                                file.FileName,

                                patient
                                    .NationalCode,

                                studyID,

                                nextSerial);
                }


                // =================================================
                // ایجاد Metadata
                // =================================================

                var image =
                    new RadiologyImage
                    {
                        StudyID =
                            studyID,

                        FileName =
                            Path.GetFileName(
                                relativePath),

                        RelativePath =
                            relativePath,

                        ContentType =
                            contentType,

                        Description =
                            description,

                        CreatedDate =
                            DateTime.Now
                    };


                // =================================================
                // ثبت Metadata
                // =================================================
                //
                // اگر INSERT در SQL شکست بخورد،
                // فایل فیزیکی تازه ایجادشده را حذف می‌کنیم.
                //
                // =================================================

                try
                {
                    _context
                        .RadiologyImages
                        .Add(image);


                    await _context
                        .SaveChangesAsync();
                }
                catch
                {
                    // ---------------------------------------------
                    // Cleanup فایل فیزیکی
                    // ---------------------------------------------

                    try
                    {
                        // ----------------------------------------------------
                        // مسیر فیزیکی تصویر
                        // ----------------------------------------------------
                        //
                        // StorageService فقط نام فایل مستقیم زیر RootPath
                        // را قبول می‌کند.
                        //
                        // بنابراین از FileName استفاده می‌کنیم، نه RelativePath.
                        //
                        string physicalPath =
                            _storageService
                                .GetPhysicalPath(
                                    image.RelativePath);

                        if (System.IO.File
                            .Exists(
                                physicalPath))
                        {
                            System.IO.File
                                .Delete(
                                    physicalPath);
                        }
                    }
                    catch
                    {
                        // خطای Cleanup نباید خطای اصلی
                        // Database را مخفی کند.
                    }


                    throw;
                }


                // =================================================
                // پاسخ
                // =================================================

                return Ok(new
                {
                    success = true,

                    imageID =
                        image.ImageID,

                    patientID =
                        patient.PatientID,

                    nationalCode =
                        patient.NationalCode,

                    studyID =
                        image.StudyID,

                    fileName =
                        image.FileName,

                    relativePath =
                        image.RelativePath,

                    contentType =
                        image.ContentType,

                    createdDate =
                        image.CreatedDate,

                    serial =
                        nextSerial
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "Image upload failed.",

                        error =
                            ex.Message
                    });
            }
        }


        // ========================================================
        // GET
        // دریافت فایل تصویر
        // ========================================================
        //
        // مثال:
        //
        // GET /api/radiologyimages/12
        //
        // ========================================================

        [HttpGet("{imageID:long}")]
        public async Task<IActionResult> GetImage(
            long imageID)
        {
            // ----------------------------------------------------
            // بررسی ImageID
            // ----------------------------------------------------

            if (imageID <= 0)
            {
                return BadRequest(new
                {
                    success = false,

                    message =
                        "ImageID must be greater than zero."
                });
            }


            // ----------------------------------------------------
            // دریافت Metadata
            // ----------------------------------------------------

            var image =
                await _context.RadiologyImages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        i => i.ImageID ==
                             imageID);


            if (image == null)
            {
                return NotFound(new
                {
                    success = false,

                    message =
                        "Image not found."
                });
            }


            // ----------------------------------------------------
            // Physical Path
            // ----------------------------------------------------

            string physicalPath =
                _storageService
                    .GetPhysicalPath(
                        image.RelativePath);


            // ----------------------------------------------------
            // بررسی وجود فایل
            // ----------------------------------------------------

            if (!System.IO.File.Exists(
                physicalPath))
            {
                return NotFound(new
                {
                    success = false,

                    message =
                        "Physical image file not found."
                });
            }


            // ----------------------------------------------------
            // خواندن فایل
            // ----------------------------------------------------

            byte[] fileBytes =
                await System.IO.File
                    .ReadAllBytesAsync(
                        physicalPath);


            // ----------------------------------------------------
            // ارسال فایل
            // ----------------------------------------------------

            return File(
                fileBytes,

                image.ContentType,

                image.FileName);
        }


        // ========================================================
        // GET
        // لیست تصاویر یک Study
        // ========================================================
        //
        // مثال:
        //
        // GET /api/radiologyimages/study/7
        //
        // فقط Metadata برمی‌گردد.
        //
        // ========================================================

        [HttpGet("study/{studyID:int}")]
        public async Task<IActionResult>
            GetStudyImages(
                int studyID)
        {
            // ----------------------------------------------------
            // بررسی StudyID
            // ----------------------------------------------------

            if (studyID <= 0)
            {
                return BadRequest(new
                {
                    success = false,

                    message =
                        "StudyID must be greater than zero."
                });
            }


            // ----------------------------------------------------
            // بررسی وجود Study
            // ----------------------------------------------------

            bool studyExists =
                await _context
                    .RadiologyStudies
                    .AsNoTracking()

                    .AnyAsync(
                        s => s.StudyID ==
                             studyID);


            if (!studyExists)
            {
                return NotFound(new
                {
                    success = false,

                    message =
                        "Study not found."
                });
            }


            // ----------------------------------------------------
            // دریافت Metadata تصاویر
            // ----------------------------------------------------

            var images =
                await _context
                    .RadiologyImages
                    .AsNoTracking()

                    .Where(
                        i => i.StudyID ==
                             studyID)

                    .OrderBy(
                        i => i.ImageID)

                    .Select(i => new
                    {
                        i.ImageID,

                        i.StudyID,

                        i.FileName,

                        i.RelativePath,

                        i.ContentType,
                        i.Description,

                        i.CreatedDate
                    })

                    .ToListAsync();


            // ----------------------------------------------------
            // پاسخ
            // ----------------------------------------------------

            return Ok(new
            {
                success = true,

                studyID =
                    studyID,

                count =
                    images.Count,

                images =
                    images
            });
        }


        // ========================================================
        // DELETE
        // حذف تصویر
        // ========================================================
        //
        // مثال:
        //
        // DELETE /api/radiologyimages/15
        //
        //
        // هدف:
        //
        // رکورد SQL
        //
        // و
        //
        // فایل فیزیکی
        //
        // باید تا حد ممکن هماهنگ حذف شوند.
        //
        //
        // روش کار:
        //
        // 1. فایل اصلی ابتدا Rename شده و به یک نام موقت می‌رود.
        //
        //    مثال:
        //
        //    123_7_001.jpeg
        //
        //    تبدیل می‌شود به:
        //
        //    .delete_<GUID>_123_7_001.jpeg
        //
        //
        // 2. Transaction SQL شروع می‌شود.
        //
        // 3. Metadata حذف می‌شود.
        //
        // 4. SQL Commit می‌شود.
        //
        // 5. فایل موقت حذف فیزیکی می‌شود.
        //
        //
        // اگر SQL شکست بخورد:
        //
        // فایل موقت دوباره به نام اصلی برگردانده می‌شود.
        //
        // ========================================================

        [HttpDelete("{imageID:long}")]
        public async Task<IActionResult>
            DeleteImage(
                long imageID)
        {
            // ----------------------------------------------------
            // بررسی ImageID
            // ----------------------------------------------------

            if (imageID <= 0)
            {
                return BadRequest(new
                {
                    success = false,

                    message =
                        "ImageID must be greater than zero."
                });
            }


            // ----------------------------------------------------
            // دریافت Metadata
            // ----------------------------------------------------

            var image =
                await _context
                    .RadiologyImages

                    .FirstOrDefaultAsync(
                        i => i.ImageID ==
                             imageID);


            if (image == null)
            {
                return NotFound(new
                {
                    success = false,

                    message =
                        "Image not found."
                });
            }


            // ----------------------------------------------------
            // مسیر فایل اصلی
            // ----------------------------------------------------
            //
            // RelativePath مسیر واقعی فایل را زیر RootPath مشخص می‌کند.
            string originalPhysicalPath =
                _storageService
                    .GetPhysicalPath(
                        image.RelativePath);

            // ----------------------------------------------------
            // اگر فایل فیزیکی پیدا نشد
            // ----------------------------------------------------
            //
            // رکورد SQL را خودکار حذف نمی‌کنیم.
            //
            // چون این وضعیت باید ابتدا بررسی شود.
            //
            // ----------------------------------------------------

            if (!System.IO.File.Exists(
                originalPhysicalPath))
            {
                return Conflict(new
                {
                    success = false,

                    message =
                        "Physical image file not found. Database metadata was not deleted."
                });
            }


            // ----------------------------------------------------
            // پوشه فایل
            // ----------------------------------------------------

            string? directory =
                Path.GetDirectoryName(
                    originalPhysicalPath);


            if (string.IsNullOrWhiteSpace(
                directory))
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "Could not determine the image directory."
                    });
            }


            // ----------------------------------------------------
            // نام موقت
            // ----------------------------------------------------

            string temporaryPhysicalPath =
                Path.Combine(
                    directory,

                    $".delete_{Guid.NewGuid():N}_{Path.GetFileName(originalPhysicalPath)}");


            // ----------------------------------------------------
            // Transaction
            // ----------------------------------------------------

            using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            bool fileWasRenamed =
                false;


            try
            {
                // =================================================
                // Step 1
                // Rename فایل اصلی به نام موقت
                // =================================================

                System.IO.File.Move(
                    originalPhysicalPath,

                    temporaryPhysicalPath);


                fileWasRenamed =
                    true;


                // =================================================
                // Step 2
                // حذف Metadata
                // =================================================

                _context.RadiologyImages
                    .Remove(image);


                await _context
                    .SaveChangesAsync();


                // =================================================
                // Step 3
                // Commit SQL
                // =================================================

                await transaction
                    .CommitAsync();


                // =================================================
                // Step 4
                // حذف نهایی فایل موقت
                // =================================================
                //
                // SQL در این نقطه Commit شده است.
                //
                // اگر حذف فایل موقت به دلیل Lock یا Antivirus
                // شکست بخورد، عملیات منطقی حذف همچنان انجام شده است.
                //
                // در این حالت Cleanup Warning برمی‌گردانیم.
                //
                // =================================================

                bool cleanupWarning =
                    false;


                string? cleanupMessage =
                    null;


                try
                {
                    if (System.IO.File.Exists(
                        temporaryPhysicalPath))
                    {
                        System.IO.File.Delete(
                            temporaryPhysicalPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    cleanupWarning =
                        true;


                    cleanupMessage =
                        cleanupEx.Message;
                }


                // =================================================
                // پاسخ موفق
                // =================================================

                return Ok(new
                {
                    success = true,

                    imageID =
                        imageID,

                    studyID =
                        image.StudyID,

                    fileName =
                        image.FileName,

                    cleanupWarning =
                        cleanupWarning,

                    cleanupMessage =
                        cleanupMessage,

                    message =
                        cleanupWarning
                            ? "Image metadata was deleted, but temporary file cleanup could not be completed."
                            : "Image deleted successfully."
                });
            }
            catch (Exception ex)
            {
                // =================================================
                // Rollback SQL
                // =================================================

                try
                {
                    await transaction
                        .RollbackAsync();
                }
                catch
                {
                    // خطای Rollback نباید خطای اصلی را مخفی کند.
                }


                // =================================================
                // Restore فایل فیزیکی
                // =================================================
                //
                // اگر فایل به نام موقت Rename شده ولی SQL شکست
                // خورده است، آن را به نام قبلی برمی‌گردانیم.
                //
                // =================================================

                try
                {
                    if (fileWasRenamed &&
                        System.IO.File.Exists(
                            temporaryPhysicalPath) &&
                        !System.IO.File.Exists(
                            originalPhysicalPath))
                    {
                        System.IO.File.Move(
                            temporaryPhysicalPath,

                            originalPhysicalPath);
                    }
                }
                catch
                {
                    // خطای Restore فایل نباید Exception اصلی
                    // را مخفی کند.
                }


                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "Image delete failed.",

                        error =
                            ex.Message
                    });
            }
        }


        // ========================================================
        // Helper
        // بررسی Signature واقعی تصویر
        // ========================================================
        //
        // JPEG:
        //
        // FF D8 FF
        //
        //
        // PNG:
        //
        // 89 50 4E 47 0D 0A 1A 0A
        //
        // ========================================================

        private static async Task<bool>
            HasValidImageSignatureAsync(
                IFormFile file,
                string extension)
        {
            // ----------------------------------------------------
            // حداقل Header موردنیاز
            // ----------------------------------------------------

            byte[] header =
                new byte[8];


            using Stream stream =
                file.OpenReadStream();


            int bytesRead =
                await stream.ReadAsync(
                    header.AsMemory(
                        0,
                        header.Length));


            // ----------------------------------------------------
            // JPEG
            // ----------------------------------------------------

            if (extension == ".jpg" ||
                extension == ".jpeg")
            {
                if (bytesRead < 3)
                {
                    return false;
                }


                return
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF;
            }


            // ----------------------------------------------------
            // PNG
            // ----------------------------------------------------

            if (extension == ".png")
            {
                if (bytesRead < 8)
                {
                    return false;
                }


                return
                    header[0] == 0x89 &&
                    header[1] == 0x50 &&
                    header[2] == 0x4E &&
                    header[3] == 0x47 &&
                    header[4] == 0x0D &&
                    header[5] == 0x0A &&
                    header[6] == 0x1A &&
                    header[7] == 0x0A;
            }


            return false;
        }
    }
}