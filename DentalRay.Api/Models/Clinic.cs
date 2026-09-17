using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    [Table("tblClinics")]
    public class Clinic
    {
        [Key]
        public int ClinicID { get; set; }

        [Required, MaxLength(200)]
        public string ClinicName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public bool IsActive { get; set; }
    }
}
