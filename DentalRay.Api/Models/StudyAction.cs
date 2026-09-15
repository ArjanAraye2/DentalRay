using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // یک خدمت/اقدام انجام‌شده در Study. مبلغ در لحظه ثبت نگهداری می‌شود.
    [Table("tblStudyActions")]
    public class StudyAction
    {
        [Key] public int StudyActionID { get; set; }
        public int StudyID { get; set; }
        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
        [Column(TypeName = "decimal(18,0)")] public decimal Amount { get; set; }
        [MaxLength(1000)] public string? Description { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}