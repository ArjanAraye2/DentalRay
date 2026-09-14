using System.Text.RegularExpressions;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // ============================================================
    // PatientsController
    // ============================================================
    //
    // مسئول عملیات مربوط به Patient است:
    //
    // - دریافت بیمار
    // - ایجاد بیمار
    // - ویرایش بیمار
    // - تغییر NationalCode
    // - غیرفعال کردن بیمار
    // - Merge دو بیمار
    //
    // ============================================================

    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        // --------------------------------------------------------
        // DbContext
        // --------------------------------------------------------
        //
        // برای ارتباط با SQL Server از طریق EF Core استفاده می‌شود.
        private readonly DentalRayDbContext _context;


        // --------------------------------------------------------
        // Storage Service
        // --------------------------------------------------------
        //
        // برای Rename فایل‌های تصاویر هنگام:
        //
        // - تغییر NationalCode
        // - Merge
        //
        // استفاده می‌شود.
        private readonly RadiologyStorageService _storageService;


        // ========================================================
        // Constructor
        // ========================================================

        public PatientsController(
            DentalRayDbContext context,
            RadiologyStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        // ========================================================
        // GET
        // دریافت لیست بیماران / جستجوی بیمار
        // ========================================================
        //
        // نمونه‌ها:
        //
        // دریافت بیماران فعال:
        //
        // GET /api/patients
        //
        //
        // جستجو:
        //
        // GET /api/patients?search=نمازی
        //
        // GET /api/patients?search=1860271855
        //
        // GET /api/patients?search=0912
        //
        //
        // اگر بخواهیم بیماران غیرفعال نیز نمایش داده شوند:
        //
        // GET /api/patients?includeInactive=true
        //
        //
        // پارامتر search می‌تواند روی این فیلدها جستجو کند:
        //
        // - NationalCode
        // - FirstName
        // - LastName
        // - Mobile
        //
        // به صورت پیش‌فرض بیماران غیرفعال نمایش داده نمی‌شوند.
        // ========================================================

        [HttpGet]
        public async Task<IActionResult> GetPatients(
            string? search = null,
            bool includeInactive = false)
        {
            // ----------------------------------------------------
            // شروع Query
            // ----------------------------------------------------
            //
            // در این مرحله هنوز SQL اجرا نشده است.
            //
            // IQueryable به ما اجازه می‌دهد شرط‌های مختلف را
            // مرحله‌به‌مرحله به Query اضافه کنیم.
            var query =
                _context.Patients
                    .AsNoTracking()
                    .AsQueryable();


            // ----------------------------------------------------
            // فیلتر بیمار فعال
            // ----------------------------------------------------
            //
            // به صورت پیش‌فرض فقط بیماران فعال نمایش داده می‌شوند.
            //
            // اگر includeInactive = true باشد،
            // بیماران غیرفعال نیز در نتیجه خواهند بود.
            if (!includeInactive)
            {
                query =
                    query.Where(
                        p => p.IsActive);
            }


            // ----------------------------------------------------
            // جستجو
            // ----------------------------------------------------
            //
            // اگر search خالی نباشد، عبارت موردنظر را در چند
            // فیلد مختلف جستجو می‌کنیم.
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Space ابتدا و انتهای عبارت حذف می‌شود.
                string searchText =
                    search.Trim();


                query =
                    query.Where(p =>

                        // جستجو در NationalCode
                        p.NationalCode.Contains(searchText)

                        ||

                        // جستجو در FirstName
                        p.FirstName.Contains(searchText)

                        ||

                        // جستجو در LastName
                        p.LastName.Contains(searchText)

                        ||

                        // Mobile می‌تواند NULL باشد،
                        // بنابراین ابتدا NULL نبودن آن را بررسی می‌کنیم.
                        (p.Mobile != null &&
                         p.Mobile.Contains(searchText))
                    );
            }


            // ----------------------------------------------------
            // مرتب‌سازی و اجرای Query
            // ----------------------------------------------------
            //
            // برای جلوگیری از برگرداندن تعداد بسیار زیاد رکوردها
            // فعلاً حداکثر 100 بیمار برمی‌گردانیم.
            //
            // بعداً اگر لازم شد Paging اضافه می‌کنیم.
            var patients =
                await query

                    // مرتب‌سازی بر اساس نام خانوادگی
                    .OrderBy(
                        p => p.LastName)

                    // سپس نام
                    .ThenBy(
                        p => p.FirstName)

                    // حداکثر 100 رکورد
                    .Take(100)

                    // فقط اطلاعات موردنیاز Frontend را برمی‌گردانیم.
                    .Select(p => new
                    {
                        p.PatientID,
                        p.NationalCode,
                        p.FirstName,
                        p.LastName,
                        p.BirthDate,
                        p.Gender,
                        p.Mobile,
                        p.IsActive,
                        p.CreatedDate,
                        p.ModifiedDate
                    })

                    .ToListAsync();


            // ----------------------------------------------------
            // پاسخ API
            // ----------------------------------------------------

            return Ok(new
            {
                success = true,

                // تعداد رکوردهای برگشتی
                count = patients.Count,

                // لیست بیماران
                patients = patients
            });
        }
        // ========================================================
        // GET
        // دریافت بیمار بر اساس NationalCode
        // ========================================================
        //
        // مثال:
        //
        // GET /api/patients/1860271855
        //
        // ========================================================

        [HttpGet("{nationalCode}")]
        public async Task<IActionResult> GetPatient(
            string nationalCode)
        {
            var patient = await _context.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.NationalCode == nationalCode);

            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Patient not found."
                });
            }

            return Ok(patient);
        }


        // ========================================================
        // POST
        // ایجاد بیمار جدید
        // ========================================================

        [HttpPost]
        public async Task<IActionResult> CreatePatient(
            Patient patient)
        {
            try
            {
                // حذف Spaceهای ابتدا و انتهای NationalCode
                patient.NationalCode =
                    patient.NationalCode.Trim();


                // ------------------------------------------------
                // NationalCode فقط باید عدد باشد
                // ------------------------------------------------

                if (!Regex.IsMatch(
                    patient.NationalCode,
                    @"^\d+$"))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            "NationalCode must contain only digits."
                    });
                }


                // ------------------------------------------------
                // بررسی تکراری نبودن NationalCode
                // ------------------------------------------------

                bool exists =
                    await _context.Patients.AnyAsync(
                        p => p.NationalCode ==
                             patient.NationalCode);

                if (exists)
                {
                    return Conflict(new
                    {
                        success = false,
                        message =
                            "A patient with this NationalCode already exists."
                    });
                }


                // ------------------------------------------------
                // مقادیر سیستمی
                // ------------------------------------------------

                // PatientID توسط SQL Server تولید می‌شود.
                patient.PatientID = 0;

                // تاریخ ایجاد رکورد.
                patient.CreatedDate = DateTime.Now;

                // رکورد تازه ایجاد شده هنوز ویرایش نشده است.
                patient.ModifiedDate = null;

                // بیمار جدید فعال است.
                patient.IsActive = true;


                // ------------------------------------------------
                // ذخیره بیمار
                // ------------------------------------------------

                _context.Patients.Add(patient);

                await _context.SaveChangesAsync();

                return Ok(patient);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // ========================================================
        // PUT
        // ویرایش اطلاعات بیمار
        // ========================================================
        //
        // مثال:
        //
        // PUT /api/patients/1
        //
        // اگر NationalCode تغییر نکرده باشد:
        //
        // فقط اطلاعات بیمار Update می‌شود.
        //
        // اگر NationalCode تغییر کند:
        //
        // 1. بررسی می‌شود NationalCode جدید تکراری نباشد.
        //
        // 2. تمام Studyهای بیمار پیدا می‌شوند.
        //
        // 3. تمام تصاویر آن Studyها پیدا می‌شوند.
        //
        // 4. فایل‌های فیزیکی Rename می‌شوند.
        //
        // مثال:
        //
        // 1860271855_7_001.jpeg
        //
        // تبدیل می‌شود به:
        //
        // 1234567890_7_001.jpeg
        //
        // 5. FileName و RelativePath در SQL اصلاح می‌شوند.
        //
        // 6. NationalCode بیمار تغییر می‌کند.
        //
        // اگر هر مرحله خطا بدهد:
        //
        // - Transaction دیتابیس Rollback می‌شود.
        // - فایل‌های Rename شده نیز تا حد ممکن برگردانده می‌شوند.
        //
        // ========================================================

        [HttpPut("{patientID}")]
        public async Task<IActionResult> UpdatePatient(
            int patientID,
            UpdatePatientRequest request)
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
            // پاک کردن Space ابتدا و انتهای NationalCode
            // ----------------------------------------------------

            string newNationalCode =
                request.NationalCode.Trim();


            // ----------------------------------------------------
            // بررسی عددی بودن NationalCode
            // ----------------------------------------------------

            if (!Regex.IsMatch(
                newNationalCode,
                @"^\d+$"))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "NationalCode must contain only digits."
                });
            }


            // ----------------------------------------------------
            // دریافت بیمار
            // ----------------------------------------------------

            var patient =
                await _context.Patients
                    .FirstOrDefaultAsync(
                        p => p.PatientID == patientID);

            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Patient not found."
                });
            }


            // ----------------------------------------------------
            // بررسی اینکه NationalCode تغییر کرده یا خیر
            // ----------------------------------------------------

            bool nationalCodeChanged =
                !string.Equals(
                    patient.NationalCode,
                    newNationalCode,
                    StringComparison.Ordinal);


            // ----------------------------------------------------
            // اگر NationalCode تغییر کرده باشد،
            // Unique بودن مقدار جدید را بررسی می‌کنیم.
            // ----------------------------------------------------

            if (nationalCodeChanged)
            {
                bool duplicate =
                    await _context.Patients
                        .AnyAsync(
                            p =>
                                p.PatientID != patientID &&
                                p.NationalCode ==
                                newNationalCode);

                if (duplicate)
                {
                    // اگر NationalCode متعلق به بیمار دیگری است،
                    // PUT نباید اطلاعات را ادغام کند.
                    //
                    // در این حالت باید عملیات Merge استفاده شود.
                    return Conflict(new
                    {
                        success = false,
                        message =
                            "The new NationalCode already belongs to another patient. Use the Merge operation if these records represent the same patient."
                    });
                }
            }


            // ----------------------------------------------------
            // لیست Renameهای انجام‌شده
            // ----------------------------------------------------
            //
            // اگر وسط عملیات خطایی رخ دهد،
            // از این لیست برای برگرداندن نام فایل‌ها استفاده می‌کنیم.
            var renamedFiles =
                new List<(string OldName, string NewName)>();


            // ----------------------------------------------------
            // شروع Transaction دیتابیس
            // ----------------------------------------------------

            using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                // =================================================
                // اگر NationalCode تغییر کرده باشد
                // =================================================

                if (nationalCodeChanged)
                {
                    // ---------------------------------------------
                    // دریافت StudyIDهای بیمار
                    // ---------------------------------------------

                    var studyIDs =
                        await _context.RadiologyStudies
                            .Where(
                                s => s.PatientID ==
                                     patientID)
                            .Select(
                                s => s.StudyID)
                            .ToListAsync();


                    // ---------------------------------------------
                    // دریافت تصاویر تمام Studyهای بیمار
                    // ---------------------------------------------

                    var images =
                        studyIDs.Count == 0
                            ? new List<RadiologyImage>()
                            : await _context
                                .RadiologyImages
                                .Where(
                                    i => studyIDs.Contains(
                                        i.StudyID))
                                .ToListAsync();


                    // ---------------------------------------------
                    // Rename تصاویر
                    // ---------------------------------------------

                    foreach (var image in images)
                    {
                        // نام فایل فعلی
                        string oldFileName =
                            Path.GetFileName(
                                image.RelativePath);


                        // پسوند فایل
                        string extension =
                            Path.GetExtension(
                                oldFileName)
                            .ToLowerInvariant();


                        // -----------------------------------------
                        // بررسی ساختار نام فایل
                        // -----------------------------------------
                        //
                        // انتظار داریم:
                        //
                        // NationalCode_StudyID_Serial.ext
                        //
                        // مثال:
                        //
                        // 1860271855_7_001.jpeg
                        //
                        string pattern =
                            "^" +
                            Regex.Escape(
                                patient.NationalCode) +
                            "_" +
                            image.StudyID +
                            @"_(\d{3})\.(jpg|jpeg|png)$";


                        Match match =
                            Regex.Match(
                                oldFileName,
                                pattern,
                                RegexOptions.IgnoreCase);


                        if (!match.Success)
                        {
                            throw new
                                InvalidOperationException(
                                    $"Image file name is not in the expected format: {oldFileName}");
                        }


                        // -----------------------------------------
                        // استخراج Serial
                        // -----------------------------------------

                        int serial =
                            int.Parse(
                                match.Groups[1].Value);


                        // -----------------------------------------
                        // ساخت نام جدید
                        // -----------------------------------------

                        string newFileName =
                            $"{newNationalCode}_{image.StudyID}_{serial:000}{extension}";


                        // -----------------------------------------
                        // Rename فایل فیزیکی
                        // -----------------------------------------

                        _storageService.RenameImage(
                            oldFileName,
                            newFileName);


                        // ثبت برای Rollback احتمالی
                        renamedFiles.Add(
                            (oldFileName,
                             newFileName));


                        // -----------------------------------------
                        // اصلاح Metadata
                        // -----------------------------------------

                        image.FileName =
                            newFileName;

                        image.RelativePath =
                            newFileName;
                    }
                }


                // =================================================
                // ویرایش اطلاعات بیمار
                // =================================================

                patient.NationalCode =
                    newNationalCode;

                patient.FirstName =
                    request.FirstName.Trim();

                patient.LastName =
                    request.LastName.Trim();

                patient.BirthDate =
                    request.BirthDate;

                patient.Gender =
                    request.Gender;

                patient.Mobile =
                    request.Mobile;

                patient.Address =
                    request.Address;

                patient.Description =
                    request.Description;

                patient.ModifiedDate =
                    DateTime.Now;


                // -------------------------------------------------
                // ذخیره تغییرات SQL
                // -------------------------------------------------

                await _context.SaveChangesAsync();


                // -------------------------------------------------
                // Commit Transaction
                // -------------------------------------------------

                await transaction.CommitAsync();


                return Ok(new
                {
                    success = true,

                    patient = patient,

                    nationalCodeChanged =
                        nationalCodeChanged,

                    renamedImages =
                        renamedFiles.Count
                });
            }
            catch (Exception ex)
            {
                // =================================================
                // Rollback دیتابیس
                // =================================================

                await transaction.RollbackAsync();


                // =================================================
                // Rollback فایل‌های Rename شده
                // =================================================
                //
                // از آخر به اول حرکت می‌کنیم.
                //
                for (int i =
                    renamedFiles.Count - 1;
                    i >= 0;
                    i--)
                {
                    try
                    {
                        var item =
                            renamedFiles[i];


                        string oldPath =
                            _storageService
                                .GetPhysicalPath(
                                    item.OldName);


                        string newPath =
                            _storageService
                                .GetPhysicalPath(
                                    item.NewName);


                        if (System.IO.File.Exists(
                                newPath) &&
                            !System.IO.File.Exists(
                                oldPath))
                        {
                            System.IO.File.Move(
                                newPath,
                                oldPath);
                        }
                    }
                    catch
                    {
                        // اگر Rollback فیزیکی خودش خطا داشت،
                        // خطای اصلی را مخفی نمی‌کنیم.
                    }
                }


                return StatusCode(500, new
                {
                    success = false,

                    message =
                        "Patient update failed. Database changes were rolled back and the system attempted to restore renamed image files.",

                    error =
                        ex.Message
                });
            }
        }


        // ========================================================
        // PUT
        // غیرفعال کردن بیمار
        // ========================================================
        //
        // DELETE فیزیکی انجام نمی‌دهیم.
        //
        // فقط:
        //
        // IsActive = false
        //
        // می‌شود.
        //
        // ========================================================

        [HttpPut("{patientID}/deactivate")]
        public async Task<IActionResult> DeactivatePatient(
            int patientID)
        {
            var patient =
                await _context.Patients
                    .FirstOrDefaultAsync(
                        p => p.PatientID ==
                             patientID);

            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "Patient not found."
                });
            }


            // اگر قبلاً غیرفعال شده است،
            // عملیات را موفق تلقی می‌کنیم.
            if (!patient.IsActive)
            {
                return Ok(new
                {
                    success = true,

                    message =
                        "Patient is already inactive.",

                    patientID =
                        patient.PatientID
                });
            }


            patient.IsActive = false;

            patient.ModifiedDate =
                DateTime.Now;


            await _context.SaveChangesAsync();


            return Ok(new
            {
                success = true,

                patientID =
                    patient.PatientID,

                nationalCode =
                    patient.NationalCode,

                isActive =
                    patient.IsActive,

                modifiedDate =
                    patient.ModifiedDate
            });
        }

        // ========================================================
        // PUT
        // فعال کردن مجدد بیمار
        // ========================================================
        //
        // مثال:
        //
        // PUT /api/patients/7/activate
        //
        // این متد برای بیمارانی استفاده می‌شود که قبلاً:
        //
        // IsActive = false
        //
        // شده‌اند.
        //
        // بیمار حذف نشده است و فقط دوباره:
        //
        // IsActive = true
        //
        // خواهد شد.
        // ========================================================

        [HttpPut("{patientID}/activate")]
        public async Task<IActionResult> ActivatePatient(
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
            // دریافت Patient
            // ----------------------------------------------------

            var patient =
                await _context.Patients
                    .FirstOrDefaultAsync(
                        p => p.PatientID ==
                             patientID);


            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "Patient not found."
                });
            }


            // ----------------------------------------------------
            // اگر بیمار از قبل فعال است،
            // نیازی به تغییر نداریم.
            // ----------------------------------------------------

            if (patient.IsActive)
            {
                return Ok(new
                {
                    success = true,

                    message =
                        "Patient is already active.",

                    patientID =
                        patient.PatientID,

                    nationalCode =
                        patient.NationalCode,

                    isActive =
                        patient.IsActive,

                    modifiedDate =
                        patient.ModifiedDate
                });
            }


            // ----------------------------------------------------
            // فعال‌سازی بیمار
            // ----------------------------------------------------

            patient.IsActive = true;

            patient.ModifiedDate =
                DateTime.Now;


            // ----------------------------------------------------
            // ذخیره در SQL Server
            // ----------------------------------------------------

            await _context.SaveChangesAsync();


            // ----------------------------------------------------
            // پاسخ
            // ----------------------------------------------------

            return Ok(new
            {
                success = true,

                patientID =
                    patient.PatientID,

                nationalCode =
                    patient.NationalCode,

                isActive =
                    patient.IsActive,

                modifiedDate =
                    patient.ModifiedDate,

                message =
                    "Patient activated successfully."
            });
        }

        // ========================================================
        // GET
        // دریافت جزئیات کامل بیمار برای Frontend
        // ========================================================
        //
        // مثال:
        //
        // GET /api/patients/7/details
        //
        // پاسخ شامل:
        //
        // - اطلاعات خود بیمار
        // - تعداد Studyها
        // - لیست Studyها
        // - تعداد تصاویر هر Study
        //
        // توجه:
        //
        // خود فایل تصویر در این Endpoint ارسال نمی‌شود.
        // برای دریافت فایل واقعی تصویر، Frontend باید از:
        //
        // GET /api/radiologyimages/{imageID}
        //
        // استفاده کند.
        //
        // این طراحی باعث می‌شود پاسخ این Endpoint سبک باقی بماند.
        // ========================================================

        [HttpGet("{patientID}/details")]
        public async Task<IActionResult> GetPatientDetails(
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
            // دریافت Patient
            // ----------------------------------------------------

            var patient =
                await _context.Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.PatientID ==
                             patientID);


            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "Patient not found."
                });
            }


            // ----------------------------------------------------
            // دریافت Studyهای بیمار
            // ----------------------------------------------------

            var studies =
                await _context.RadiologyStudies
                    .AsNoTracking()
                    .Where(
                        s => s.PatientID ==
                             patientID)

                    .OrderByDescending(
                        s => s.StudyDate)

                    .ThenByDescending(
                        s => s.StudyID)

                    .ToListAsync();


            // ----------------------------------------------------
            // دریافت StudyIDها
            // ----------------------------------------------------

            var studyIDs =
                studies
                    .Select(
                        s => s.StudyID)
                    .ToList();


            // ----------------------------------------------------
            // دریافت تعداد تصاویر هر Study
            // ----------------------------------------------------
            //
            // به جای اینکه برای هر Study یک Query جداگانه
            // به SQL Server بفرستیم، همه Imageها را یکجا
            // Group می‌کنیم.
            //
            var imageCounts =
                studyIDs.Count == 0
                    ? new Dictionary<int, int>()
                    : await _context.RadiologyImages
                        .AsNoTracking()
                        .Where(
                            i => studyIDs.Contains(
                                i.StudyID))

                        .GroupBy(
                            i => i.StudyID)

                        .Select(g => new
                        {
                            StudyID = g.Key,
                            Count = g.Count()
                        })

                        .ToDictionaryAsync(
                            x => x.StudyID,
                            x => x.Count);


            // ----------------------------------------------------
            // آماده کردن Studyها برای Frontend
            // ----------------------------------------------------

            var studyList =
                studies.Select(
                    study => new
                    {
                        study.StudyID,
                        study.PatientID,
                        study.StudyDate,
                        study.StudyType,
                        study.BodyPart,
                        study.Description,
                        study.Report,
                        study.CreatedDate,
                        study.ModifiedDate,

                        // تعداد تصاویر Study
                        imageCount =
                            imageCounts.TryGetValue(
                                study.StudyID,
                                out int count)
                                ? count
                                : 0
                    })
                .ToList();


            // ----------------------------------------------------
            // تعداد کل تصاویر بیمار
            // ----------------------------------------------------

            int totalImageCount =
                imageCounts.Values.Sum();


            // ----------------------------------------------------
            // پاسخ نهایی
            // ----------------------------------------------------

            return Ok(new
            {
                success = true,

                patient = new
                {
                    patient.PatientID,
                    patient.NationalCode,
                    patient.FirstName,
                    patient.LastName,
                    patient.BirthDate,
                    patient.Gender,
                    patient.Mobile,
                    patient.Address,
                    patient.Description,
                    patient.CreatedDate,
                    patient.ModifiedDate,
                    patient.IsActive
                },

                studyCount =
                    studyList.Count,

                totalImageCount =
                    totalImageCount,

                studies =
                    studyList
            });
        }

        // ========================================================
        // POST
        // Merge بیماران
        // ========================================================
        //
        // Source:
        // رکورد اشتباه / تکراری
        //
        // Target:
        // رکورد صحیح
        //
        // عملیات:
        //
        // 1. تصاویر Source Rename می‌شوند.
        // 2. Metadata تصاویر Update می‌شود.
        // 3. Studyها به Target منتقل می‌شوند.
        // 4. Source حذف فیزیکی می‌شود.
        //
        // ========================================================

        [HttpPost("merge")]
        public async Task<IActionResult> MergePatients(
            MergePatientRequest request)
        {
            // ----------------------------------------------------
            // بررسی IDها
            // ----------------------------------------------------

            if (request.SourcePatientID <= 0 ||
                request.TargetPatientID <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "SourcePatientID and TargetPatientID must be greater than zero."
                });
            }


            if (request.SourcePatientID ==
                request.TargetPatientID)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "SourcePatientID and TargetPatientID must be different."
                });
            }


            // ----------------------------------------------------
            // دریافت Source
            // ----------------------------------------------------

            var sourcePatient =
                await _context.Patients
                    .FirstOrDefaultAsync(
                        p => p.PatientID ==
                             request.SourcePatientID);

            if (sourcePatient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "Source patient not found."
                });
            }


            // ----------------------------------------------------
            // دریافت Target
            // ----------------------------------------------------

            var targetPatient =
                await _context.Patients
                    .FirstOrDefaultAsync(
                        p => p.PatientID ==
                             request.TargetPatientID);

            if (targetPatient == null)
            {
                return NotFound(new
                {
                    success = false,
                    message =
                        "Target patient not found."
                });
            }


            // ----------------------------------------------------
            // دریافت Studyهای Source
            // ----------------------------------------------------

            var sourceStudies =
                await _context.RadiologyStudies
                    .Where(
                        s => s.PatientID ==
                             request.SourcePatientID)
                    .ToListAsync();


            var studyIDs =
                sourceStudies
                    .Select(
                        s => s.StudyID)
                    .ToList();


            // ----------------------------------------------------
            // دریافت تصاویر Source
            // ----------------------------------------------------

            var sourceImages =
                studyIDs.Count == 0
                    ? new List<RadiologyImage>()
                    : await _context
                        .RadiologyImages
                        .Where(
                            i => studyIDs.Contains(
                                i.StudyID))
                        .ToListAsync();


            var renamedFiles =
                new List<(string OldName,
                          string NewName)>();


            using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                // =================================================
                // Rename تصاویر
                // =================================================

                foreach (var image
                         in sourceImages)
                {
                    string oldFileName =
                        Path.GetFileName(
                            image.RelativePath);


                    string extension =
                        Path.GetExtension(
                            oldFileName)
                        .ToLowerInvariant();


                    string pattern =
                        "^" +
                        Regex.Escape(
                            sourcePatient
                                .NationalCode) +
                        "_" +
                        image.StudyID +
                        @"_(\d{3})\.(jpg|jpeg|png)$";


                    Match match =
                        Regex.Match(
                            oldFileName,
                            pattern,
                            RegexOptions.IgnoreCase);


                    if (!match.Success)
                    {
                        throw new
                            InvalidOperationException(
                                $"Image file name is not in the expected format: {oldFileName}");
                    }


                    int serial =
                        int.Parse(
                            match.Groups[1].Value);


                    string newFileName =
                        $"{targetPatient.NationalCode}_{image.StudyID}_{serial:000}{extension}";


                    // Rename فیزیکی
                    _storageService.RenameImage(
                        oldFileName,
                        newFileName);


                    renamedFiles.Add(
                        (oldFileName,
                         newFileName));


                    // Update Metadata
                    image.FileName =
                        newFileName;

                    image.RelativePath =
                        newFileName;
                }


                // =================================================
                // انتقال Studyها
                // =================================================

                foreach (var study
                         in sourceStudies)
                {
                    study.PatientID =
                        request.TargetPatientID;

                    study.ModifiedDate =
                        DateTime.Now;
                }


                // =================================================
                // حذف Source
                // =================================================

                _context.Patients.Remove(
                    sourcePatient);


                // =================================================
                // ذخیره SQL
                // =================================================

                await _context.SaveChangesAsync();


                // =================================================
                // Commit
                // =================================================

                await transaction.CommitAsync();


                return Ok(new
                {
                    success = true,

                    sourcePatientID =
                        request.SourcePatientID,

                    targetPatientID =
                        request.TargetPatientID,

                    transferredStudies =
                        sourceStudies.Count,

                    renamedImages =
                        renamedFiles.Count,

                    sourceNationalCode =
                        sourcePatient.NationalCode,

                    targetNationalCode =
                        targetPatient.NationalCode,

                    message =
                        "Patients merged successfully and image files were renamed."
                });
            }
            catch (Exception ex)
            {
                // =================================================
                // Rollback SQL
                // =================================================

                await transaction.RollbackAsync();


                // =================================================
                // Rollback فایل‌ها
                // =================================================

                for (int i =
                    renamedFiles.Count - 1;
                    i >= 0;
                    i--)
                {
                    try
                    {
                        var item =
                            renamedFiles[i];


                        string oldPath =
                            _storageService
                                .GetPhysicalPath(
                                    item.OldName);


                        string newPath =
                            _storageService
                                .GetPhysicalPath(
                                    item.NewName);


                        if (System.IO.File.Exists(
                                newPath) &&
                            !System.IO.File.Exists(
                                oldPath))
                        {
                            System.IO.File.Move(
                                newPath,
                                oldPath);
                        }
                    }
                    catch
                    {
                    }
                }


                return StatusCode(500, new
                {
                    success = false,

                    message =
                        "Patient merge failed. Database changes were rolled back and the system attempted to restore renamed files.",

                    error =
                        ex.Message
                });
            }
        }
    }
}