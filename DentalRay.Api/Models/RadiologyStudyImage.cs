using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // ============================================================
    // RadiologyStudyImage
    // ============================================================
    // جدول واسط رابطه Many-to-Many بین Study و Image.
    // یک Image می‌تواند به چند Study متصل شود و هر Study نیز
    // می‌تواند چند Image داشته باشد.
    // ============================================================
    [Table("tblRadiologyStudyImages")]
    public class RadiologyStudyImage
    {
        public int StudyID { get; set; }
        public long ImageID { get; set; }

        // زمان ایجاد Link برای Audit و توسعه‌های آینده نگهداری می‌شود.
        public DateTime CreatedDate { get; set; }
    }
}
