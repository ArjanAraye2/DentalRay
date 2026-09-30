using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models
{
    [Table("tblSpecialties")]
    public class Specialty
    {
        [Key]
        public int SpecialtyID { get; set; }

        [Required, MaxLength(150)]
        public string SpecialtyName { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
