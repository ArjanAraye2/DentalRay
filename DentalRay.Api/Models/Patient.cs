using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // این کلاس نماینده یک بیمار در برنامه DentalRay است.
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

        // مالک پروفایل بیمار؛ دسترسی به Studyهای اشتراکی جداگانه ارزیابی می‌شود.
        public int? OwnerUserID { get; set; }


        // NationalCode شناسه یکتای بیمار در DentalRay است.
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
    }
}
