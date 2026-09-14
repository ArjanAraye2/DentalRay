using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    [Table("tblUsers")]
    public class AppUser
    {
        [Key]
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = "User";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }
}
