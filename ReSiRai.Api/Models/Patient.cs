using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models
{
    // این کلاس نماینده یک بیمار در برنامه ReSiRai است.
    //
    // EF Core از این کلاس برای ارتباط با جدول tblPatients
    // در دیتابیس SQL Server استفاده می‌کند.
    [Table("tblPatients")]
    public class Patient
    {
        // PatientID کلید داخلی جدول است.
        //
        // مقدار آن توسط SQL Server ایجاد می‌شود
        // و برنامه هنگام ثبت بیمار نباید آن را تعیین کند.
        [Key]
        public int PatientID { get; set; }


        // NationalCode شناسه یکتای بیمار در ReSiRai است.
        //
        // در دیتابیس نیز روی این فیلد محدودیت UNIQUE داریم،
        // بنابراین دو بیمار نمی‌توانند NationalCode یکسان داشته باشند.
        [Required]
        [MaxLength(20)]
        public string NationalCode { get; set; } = string.Empty;


        // نام بیمار
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;


        // نام خانوادگی بیمار
        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;


        // تاریخ تولد بیمار
        //
        // علامت ? یعنی این مقدار می‌تواند خالی (NULL) باشد.
        //
        // توجه:
        // تاریخ در SQL Server به صورت استاندارد ذخیره می‌شود.
        // نمایش شمسی آن را بعداً در Frontend انجام خواهیم داد.
        public DateTime? BirthDate { get; set; }


        // جنسیت بیمار
        //
        // فعلاً به صورت یک مقدار عددی کوچک ذخیره می‌شود.
        // مقدار ? یعنی می‌تواند NULL باشد.
        public byte? Gender { get; set; }


        // شماره موبایل بیمار
        //
        // فعلاً فقط همین فیلد را برای اطلاعات تماس نگه داشته‌ایم.
        [MaxLength(30)]
        public string? Mobile { get; set; }


        // آدرس بیمار
        [MaxLength(500)]
        public string? Address { get; set; }


        // توضیحات اضافی مربوط به بیمار
        [MaxLength(1000)]
        public string? Description { get; set; }


        // مسیر نسبی عکس پروفایل بیمار در فضای ذخیره‌سازی ReSiRai.
        // عکس اختیاری است و می‌تواند از فایل موجود یا دوربین موبایل دریافت شود.
        [MaxLength(500)]
        public string? PhotoRelativePath { get; set; }


        // تاریخ ایجاد رکورد بیمار
        //
        // این مقدار هنگام ثبت بیمار توسط برنامه تعیین می‌شود.
        public DateTime CreatedDate { get; set; }


        // تاریخ آخرین تغییر اطلاعات بیمار
        //
        // اگر بیمار هنوز ویرایش نشده باشد، مقدار آن NULL است.
        public DateTime? ModifiedDate { get; set; }


        // وضعیت فعال بودن بیمار
        //
        // true  = بیمار فعال است
        // false = بیمار غیرفعال است
        //
        // برای بیمارها حذف فیزیکی را انجام نمی‌دهیم؛
        // در صورت نیاز بیمار را غیرفعال می‌کنیم.
        public bool IsActive { get; set; }


        // ---- «اطلاعات تکمیلی» پرونده — همه اختیاری ----------------------------
        //
        // این فیلدها در سطح بیمار و مشترک‌اند (نه در سطح مراجعه). هیچ‌کدام برای
        // ثبت بیمار الزامی نیستند؛ پزشک/منشی هر زمان خواست کامل می‌کند.

        /// <summary>گروه خونی، یکی از A+/A-/B+/B-/AB+/AB-/O+/O-. خالی = ثبت‌نشده.</summary>
        [MaxLength(5)]
        public string? BloodType { get; set; }

        /// <summary>موبایل دوم / شمارهٔ تماس جایگزین.</summary>
        [MaxLength(30)]
        public string? Mobile2 { get; set; }

        // تماس اضطراری: نام، نسبت و شماره.
        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }
        [MaxLength(50)]
        public string? EmergencyContactRelation { get; set; }
        [MaxLength(30)]
        public string? EmergencyContactPhone { get; set; }

        // بیمهٔ پایه. نوع از دیکشنری بیمه (IsSupplementary = false) می‌آید.
        public int? BaseInsuranceTypeID { get; set; }
        [MaxLength(50)]
        public string? BaseInsuranceNo { get; set; }

        // دو بیمهٔ تکمیلی؛ بیمار می‌تواند هیچ‌کدام، یکی یا هر دو را داشته باشد.
        public int? Supp1InsuranceTypeID { get; set; }
        [MaxLength(50)]
        public string? Supp1InsuranceNo { get; set; }
        public int? Supp2InsuranceTypeID { get; set; }
        [MaxLength(50)]
        public string? Supp2InsuranceNo { get; set; }

        /// <summary>شمارهٔ پروندهٔ کاغذی مطب، برای تطبیق با بایگانی سنتی.</summary>
        [MaxLength(50)]
        public string? FileNumber { get; set; }

        /// <summary>sms / call / none — ترجیح بیمار برای آگاهی‌رسانی.</summary>
        [MaxLength(20)]
        public string? ContactPreference { get; set; }
    }
}