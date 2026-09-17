using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // ============================================================
    // RadiologyImage
    // ============================================================
    // یک فایل تصویری یا PDF متعلق به Patient است، نه Study.
    // فایل فیزیکی فقط یک بار ذخیره می‌شود و می‌تواند از طریق
    // tblRadiologyStudyImages به صفر، یک یا چند Study متصل شود.
    // ============================================================
    [Table("tblRadiologyImages")]
    public class RadiologyImage
    {
        [Key]
        public long ImageID { get; set; }

        // مالک دائمی فایل Patient است.
        public int PatientID { get; set; }

        // نوع بالینی تصویر (برای مثال OPG یا CBCT).
        // برای تصاویر قدیمی تا زمان تعیین نوع، مقدار می‌تواند NULL باشد.
        public int? ImageTypeID { get; set; }

        // نام استاندارد تولیدشده توسط DentalRay است؛ نام اصلی
        // Upload شده طبق تصمیم طراحی نگهداری نمی‌شود.
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        // مثال:
        // 1860271855\1860271855_14050626_001.jpg
        [Required]
        [MaxLength(1000)]
        public string RelativePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ContentType { get; set; } = string.Empty;

        // Serial برای هر Patient مستقل و افزایشی است و با تغییر
        // روز Reset نمی‌شود.
        public int SerialNumber { get; set; }

        // تاریخ/زمان واقعی Upload در SQL Server میلادی ذخیره می‌شود.
        // فقط بخش تاریخ نام فایل به Jalali تبدیل خواهد شد.
        public DateTime CreatedDate { get; set; }
    }
}
