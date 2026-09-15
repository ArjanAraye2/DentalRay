using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // این جدول، تاریخچه مالی غیرقابل‌ویرایش برنامه را نگهداری می‌کند.
    // هر ردیف یک رویداد مالی است و مقدار قبل و بعد را به‌صورت JSON ذخیره می‌کند.
    [Table("tblStudyFinancialAuditLogs")]
    public class StudyFinancialAuditLog
    {
        [Key]
        public long StudyFinancialAuditLogID { get; set; }

        public int StudyID { get; set; }

        [Required, MaxLength(30)]
        public string EntityType { get; set; } = string.Empty;

        public int? EntityID { get; set; }

        [Required, MaxLength(30)]
        public string Operation { get; set; } = string.Empty;

        public int? UserID { get; set; }

        [MaxLength(50)]
        public string? UserName { get; set; }

        public string? BeforeJson { get; set; }

        public string? AfterJson { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
