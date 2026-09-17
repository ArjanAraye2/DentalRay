using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // A DentalRay login account. Personal information belongs to tblStaff;
    // tblUsers contains only account-specific information.
    [Table("tblUsers")]
    public class User
    {
        [Key]
        public int UserID { get; set; }
        public int StaffID { get; set; }

        [Required, MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        // Never store or return the plain password. ASP.NET Core's password
        // hasher stores a salted, versioned hash in this field.
        [MaxLength(500)]
        public string? PasswordHash { get; set; }

        public bool IsActive { get; set; }
    }
}
