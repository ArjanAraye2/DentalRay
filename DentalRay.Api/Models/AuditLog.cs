using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    [Table("tblAuditLogs")]
    public class AuditLog
    {
        [Key]
        public long AuditLogID { get; set; }

        public int? UserID { get; set; }

        [MaxLength(50)]
        public string? UserName { get; set; }

        [Required, MaxLength(150)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? HttpMethod { get; set; }

        [MaxLength(500)]
        public string? Path { get; set; }

        public int? StatusCode { get; set; }
        public bool IsSuccess { get; set; } = true;

        [MaxLength(2000)]
        public string? Details { get; set; }

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
