using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    [Table("tblDentalSpecialties")]
    public class DentalSpecialty
    {
        [Key]
        public int SpecialtyID { get; set; }

        [Required, MaxLength(150)]
        public string SpecialtyName { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
