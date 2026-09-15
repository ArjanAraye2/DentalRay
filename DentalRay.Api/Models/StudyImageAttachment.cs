using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // یک تصویر اصلی می‌تواند در چند Study دیده شود؛ این جدول فقط پیوند را ثبت می‌کند.
    // مالکیت، دیدپذیری و فایل اصلی همچنان متعلق به خود تصویر باقی می‌ماند.
    [Table("tblStudyImageAttachments")]
    public sealed class StudyImageAttachment
    {
        [Key]
        public long StudyImageAttachmentID { get; set; }

        public int StudyID { get; set; }
        public long ImageID { get; set; }
        public int? AttachedByUserID { get; set; }
        public DateTime AttachedDate { get; set; } = DateTime.Now;
    }
}
