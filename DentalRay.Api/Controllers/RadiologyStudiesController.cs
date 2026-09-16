using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public class RadiologyStudiesController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly ResourceAccessService _access;


        // ========================================================
        // Constructor
        // ========================================================

        public RadiologyStudiesController(
            DentalRayDbContext context,
            ResourceAccessService access)
        {
            _context = context;
            _access = access;
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
            int currentUserID = _access.GetCurrentUserID(User);
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

                if (!await _access.CanReadPatientAsync(study.PatientID, currentUserID))
                    return NotFound(new { success = false, message = "Patient not found." });

                if (study.Visibility > ResourceAccessService.PublicVisibility)
                    return BadRequest(new { success = false, message = "Invalid visibility." });


                // ------------------------------------------------
                // بررسی مطب و دندانپزشک (نسخه MVP)
                // ------------------------------------------------
                // ستون‌ها برای سازگاری با داده‌های قدیمی Nullable مانده‌اند،
                // اما هر Study جدید باید مطب و دندانپزشک فعال داشته باشد.
                var assignmentError = await ValidateStudyAssignmentAsync(
                    study.OrganizationID,
                    study.DentistPersonID,
                    dentistPersonID => study.DentistPersonID = dentistPersonID);

                if (assignmentError != null)
                    return BadRequest(new { success = false, message = assignmentError });


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
                // تاریخ و زمان باید صریحاً توسط کاربر/Client ارسال شود.
                // جایگزین‌کردن مقدار خالی با زمان جاری می‌تواند باعث ثبت
                // اطلاعات نادرست و پنهان‌ماندن خطای فرم شود.
                //
                if (study.StudyDate == default)
                {
                    return BadRequest(new
                    {
                        success = false,

                        message =
                            "StudyDate is required."
                    });
                }


                // ------------------------------------------------
                // مقادیر سیستمی
                // ------------------------------------------------

                study.StudyID = 0;
                study.OwnerUserID = currentUserID;


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


            int currentUserID = _access.GetCurrentUserID(User);
            if (study == null ||
                !await _access.CanReadStudyAsync(studyID, currentUserID))
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

            int currentUserID = _access.GetCurrentUserID(User);
            if (!await _access.CanReadPatientAsync(patientID, currentUserID))
                return NotFound(new { success = false, message = "Patient not found." });


            // ----------------------------------------------------
            // دریافت Studyها
            // ----------------------------------------------------

            var studies =
                await (from s in _access.ReadableStudies(currentUserID).AsNoTracking()
                       join o in _context.Organizations.AsNoTracking()
                           on s.OrganizationID equals o.OrganizationID into organizations
                       from o in organizations.DefaultIfEmpty()
                       join p in _context.Persons.AsNoTracking()
                           on s.DentistPersonID equals p.PersonID into dentists
                       from p in dentists.DefaultIfEmpty()
                       where s.PatientID == patientID
                       orderby s.StudyDate descending, s.StudyID descending
                       select new
                       {
                           s.StudyID,
                           s.PatientID,
                           s.OwnerUserID,
                           s.Visibility,
                           s.OrganizationID,
                           s.DentistPersonID,
                           OrganizationName = o == null ? null : o.Name,
                           DentistName = p == null ? null : (p.FirstName + " " + p.LastName),
                           s.StudyDate,
                           s.StudyType,
                           s.BodyPart,
                           s.Description,
                           s.Report,
                           s.CreatedDate,
                           s.ModifiedDate
                       }).ToListAsync();


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

                int currentUserID = _access.GetCurrentUserID(User);
                if (study.OwnerUserID != currentUserID)
                    return NotFound(new { success = false, message = "Study not found." });


                // ------------------------------------------------
                // اعتبارسنجی مطب و دندانپزشک
                // ------------------------------------------------
                var assignmentError = await ValidateStudyAssignmentAsync(
                    request.OrganizationID,
                    request.DentistPersonID,
                    dentistPersonID => request.DentistPersonID = dentistPersonID);

                if (assignmentError != null)
                    return BadRequest(new { success = false, message = assignmentError });


                // ------------------------------------------------
                // Update اطلاعات
                // ------------------------------------------------

                study.OrganizationID = request.OrganizationID;
                study.DentistPersonID = request.DentistPersonID;

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


        // ========================================================
        // Helper
        // اعتبارسنجی ارتباط Study با مطب و دندانپزشک
        // ========================================================
        //
        // این منطق بین ثبت و ویرایش مشترک است تا قواعد دو مسیر
        // با گذشت زمان از هم جدا نشوند. اگر مطب دقیقاً یک
        // دندانپزشک فعال داشته باشد، همان شخص خودکار انتخاب می‌شود.
        // ========================================================

        private async Task<string?> ValidateStudyAssignmentAsync(
            int? organizationID,
            int? dentistPersonID,
            Action<int?> setDentistPersonID)
        {
            if (!organizationID.HasValue)
                return "Organization is required.";

            bool organizationExists = await _context.Organizations.AnyAsync(x =>
                x.OrganizationID == organizationID.Value && x.IsActive);

            if (!organizationExists)
                return "Organization not found or inactive.";

            var activeDentistIDs = await (
                from member in _context.OrganizationMembers
                join person in _context.Persons
                    on member.PersonID equals person.PersonID
                where member.OrganizationID == organizationID.Value &&
                      member.IsActive &&
                      person.IsActive
                select member.PersonID)
                .Distinct()
                .ToListAsync();

            if (activeDentistIDs.Count == 0)
                return "The organization has no active dentist.";

            if (!dentistPersonID.HasValue)
            {
                if (activeDentistIDs.Count > 1)
                    return "Dentist selection is required for organizations with multiple dentists.";

                dentistPersonID = activeDentistIDs[0];
                setDentistPersonID(dentistPersonID);
            }

            if (!activeDentistIDs.Contains(dentistPersonID.Value))
                return "Selected dentist does not belong to this organization.";

            return null;
        }
    }
}
