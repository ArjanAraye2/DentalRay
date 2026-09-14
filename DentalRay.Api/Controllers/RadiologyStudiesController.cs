using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // ============================================================
    // RadiologyStudiesController
    // ============================================================
    //
    // مسئول عملیات مربوط به Studyهای رادیولوژی است.
    //
    // قابلیت‌های این Controller:
    //
    // - ایجاد Study
    // - دریافت یک Study
    // - دریافت Studyهای یک بیمار
    // - ویرایش Study
    //
    // در این مرحله حذف Study را پیاده‌سازی نمی‌کنیم.
    //
    // دلیل:
    //
    // یک Study ممکن است چند تصویر وابسته داشته باشد.
    // بنابراین حذف Study باید همراه با یک سیاست دقیق برای
    // تصاویر وابسته انجام شود و بهتر است در صورت نیاز
    // به عنوان یک عملیات جداگانه طراحی شود.
    //
    // ============================================================

    [ApiController]
    [Route("api/[controller]")]
    public class RadiologyStudiesController : ControllerBase
    {
        private readonly DentalRayDbContext _context;


        // ========================================================
        // Constructor
        // ========================================================

        public RadiologyStudiesController(
            DentalRayDbContext context)
        {
            _context = context;
        }


        // ========================================================
        // POST
        // ایجاد Study جدید
        // ========================================================
        //
        // مثال:
        //
        // POST /api/radiologystudies
        //
        // ========================================================

        [HttpPost]
        public async Task<IActionResult> CreateStudy(
            RadiologyStudy study)
        {
            try
            {
                // ------------------------------------------------
                // بررسی PatientID
                // ------------------------------------------------

                if (study.PatientID <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "PatientID must be greater than zero."
                    });
                }


                // ------------------------------------------------
                // بررسی وجود Patient
                // ------------------------------------------------

                bool patientExists =
                    await _context.Patients
                        .AnyAsync(
                            p => p.PatientID ==
                                 study.PatientID);


                if (!patientExists)
                {
                    return NotFound(new
                    {
                        success = false,

                        message =
                            "Patient not found."
                    });
                }


                // ------------------------------------------------
                // بررسی StudyType
                // ------------------------------------------------

                if (string.IsNullOrWhiteSpace(
                    study.StudyType))
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "StudyType is required."
                    });
                }


                // ------------------------------------------------
                // پاک‌سازی اطلاعات متنی
                // ------------------------------------------------

                study.StudyType =
                    study.StudyType.Trim();


                study.BodyPart =
                    NormalizeOptionalText(
                        study.BodyPart);


                study.Description =
                    NormalizeOptionalText(
                        study.Description);


                study.Report =
                    NormalizeOptionalText(
                        study.Report);


                // ------------------------------------------------
                // بررسی طول StudyType
                // ------------------------------------------------
                //
                // این بررسی علاوه بر DataAnnotation انجام می‌شود
                // تا پیام خطای قابل‌کنترل‌تری داشته باشیم.
                //
                if (study.StudyType.Length > 50)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "StudyType cannot be longer than 50 characters."
                    });
                }


                // ------------------------------------------------
                // بررسی BodyPart
                // ------------------------------------------------

                if (study.BodyPart != null &&
                    study.BodyPart.Length > 100)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "BodyPart cannot be longer than 100 characters."
                    });
                }


                // ------------------------------------------------
                // بررسی Description
                // ------------------------------------------------

                if (study.Description != null &&
                    study.Description.Length > 1000)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "Description cannot be longer than 1000 characters."
                    });
                }


                // ------------------------------------------------
                // StudyDate
                // ------------------------------------------------
                //
                // اگر به هر دلیلی Frontend تاریخ نفرستاده باشد،
                // زمان فعلی را استفاده می‌کنیم.
                //
                if (study.StudyDate == default)
                {
                    study.StudyDate =
                        DateTime.Now;
                }


                // ------------------------------------------------
                // مقادیر سیستمی
                // ------------------------------------------------

                study.StudyID = 0;


                study.CreatedDate =
                    DateTime.Now;


                study.ModifiedDate =
                    null;


                // ------------------------------------------------
                // ثبت Study
                // ------------------------------------------------

                _context.RadiologyStudies
                    .Add(study);


                await _context
                    .SaveChangesAsync();


                // ------------------------------------------------
                // پاسخ
                // ------------------------------------------------

                return Ok(study);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,

                        message =
                            "Study creation failed.",

                        error =
                            ex.Message
                    });
            }
        }


        // ========================================================
        // GET
        // دریافت یک Study
        // ========================================================
        //
        // مثال:
        //
        // GET /api/radiologystudies/7
        //
        // ========================================================

        [HttpGet("{studyID:int}")]
        public async Task<IActionResult> GetStudy(
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
            // دریافت Study
            // ----------------------------------------------------

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


            return Ok(study);
        }


        // ========================================================
        // GET
        // دریافت Studyهای یک Patient
        // ========================================================
        //
        // مثال:
        //
        // GET /api/radiologystudies/patient/1
        //
        // ========================================================

        [HttpGet("patient/{patientID:int}")]
        public async Task<IActionResult>
            GetPatientStudies(
                int patientID)
        {
            // ----------------------------------------------------
            // بررسی PatientID
            // ----------------------------------------------------

            if (patientID <= 0)
            {
                return BadRequest(new
                {
                    success = false,

                    message =
                        "PatientID must be greater than zero."
                });
            }


            // ----------------------------------------------------
            // بررسی وجود Patient
            // ----------------------------------------------------

            bool patientExists =
                await _context.Patients
                    .AsNoTracking()
                    .AnyAsync(
                        p => p.PatientID ==
                             patientID);


            if (!patientExists)
            {
                return NotFound(new
                {
                    success = false,

                    message =
                        "Patient not found."
                });
            }


            // ----------------------------------------------------
            // دریافت Studyها
            // ----------------------------------------------------

            var studies =
                await _context.RadiologyStudies
                    .AsNoTracking()

                    .Where(
                        s => s.PatientID ==
                             patientID)

                    // جدیدترین Study در ابتدا
                    .OrderByDescending(
                        s => s.StudyDate)

                    // در تاریخ یکسان،
                    // StudyID بزرگ‌تر ابتدا
                    .ThenByDescending(
                        s => s.StudyID)

                    .ToListAsync();


            // ----------------------------------------------------
            // پاسخ
            // ----------------------------------------------------

            return Ok(new
            {
                success = true,

                patientID =
                    patientID,

                count =
                    studies.Count,

                studies =
                    studies
            });
        }


        // ========================================================
        // PUT
        // ویرایش Study
        // ========================================================
        //
        // مثال:
        //
        // PUT /api/radiologystudies/7
        //
        // موارد قابل ویرایش:
        //
        // - StudyDate
        // - StudyType
        // - BodyPart
        // - Description
        // - Report
        //
        // PatientID قابل تغییر نیست.
        //
        // انتقال Study بین بیماران فقط از طریق Merge انجام می‌شود.
        //
        // ========================================================

        [HttpPut("{studyID:int}")]
        public async Task<IActionResult> UpdateStudy(
            int studyID,
            UpdateRadiologyStudyRequest request)
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
                // بررسی Request
                // ------------------------------------------------

                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "Study information is required."
                    });
                }


                // ------------------------------------------------
                // StudyType اجباری است
                // ------------------------------------------------

                if (string.IsNullOrWhiteSpace(
                    request.StudyType))
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "StudyType is required."
                    });
                }


                // ------------------------------------------------
                // پاک‌سازی اطلاعات
                // ------------------------------------------------

                string studyType =
                    request.StudyType.Trim();


                string? bodyPart =
                    NormalizeOptionalText(
                        request.BodyPart);


                string? description =
                    NormalizeOptionalText(
                        request.Description);


                string? report =
                    NormalizeOptionalText(
                        request.Report);


                // ------------------------------------------------
                // Validation طول فیلدها
                // ------------------------------------------------

                if (studyType.Length > 50)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "StudyType cannot be longer than 50 characters."
                    });
                }


                if (bodyPart != null &&
                    bodyPart.Length > 100)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "BodyPart cannot be longer than 100 characters."
                    });
                }


                if (description != null &&
                    description.Length > 1000)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "Description cannot be longer than 1000 characters."
                    });
                }


                // ------------------------------------------------
                // بررسی تاریخ
                // ------------------------------------------------

                if (request.StudyDate == default)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "StudyDate is required."
                    });
                }


                // ------------------------------------------------
                // دریافت Study
                // ------------------------------------------------

                var study =
                    await _context.RadiologyStudies
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
                // Update اطلاعات
                // ------------------------------------------------

                study.StudyDate =
                    request.StudyDate;


                study.StudyType =
                    studyType;


                study.BodyPart =
                    bodyPart;


                study.Description =
                    description;


                study.Report =
                    report;


                study.ModifiedDate =
                    DateTime.Now;


                // ------------------------------------------------
                // ذخیره SQL
                // ------------------------------------------------

                await _context
                    .SaveChangesAsync();


                // ------------------------------------------------
                // پاسخ
                // ------------------------------------------------

                return Ok(new
                {
                    success = true,

                    study =
                        study,

                    message =
                        "Study updated successfully."
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
                            "Study update failed.",

                        error =
                            ex.Message
                    });
            }
        }


        // ========================================================
        // Helper
        // پاک‌سازی Textهای Optional
        // ========================================================
        //
        // اگر مقدار:
        //
        // null
        // ""
        // "   "
        //
        // باشد، NULL ذخیره می‌کنیم.
        //
        // در غیر این صورت Space ابتدا و انتها حذف می‌شود.
        //
        // ========================================================

        private static string?
            NormalizeOptionalText(
                string? value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return null;
            }


            return value.Trim();
        }
    }
}