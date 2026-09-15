using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // این ردیف یک اجازهٔ مشاهده/اشتراک دائمی است.
    // برای حذف یا ویرایش آن هیچ endpoint برنامه‌ای وجود ندارد.
    [Table("tblResourceAccessGrants")]
    public sealed class ResourceAccessGrant
    {
        [Key]
        public long ResourceAccessGrantID { get; set; }

        // 1 = Study، 2 = Image
        public byte ResourceType { get; set; }

        // شناسهٔ Study یا Image؛ به علت چندنوعی بودن، FK مستقیم ندارد.
        public long ResourceID { get; set; }

        public int OwnerUserID { get; set; }
        public int GrantedByUserID { get; set; }
        public int RecipientUserID { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
