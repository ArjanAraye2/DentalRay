using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // A dentist or other professional. PositionName is intentionally simple for this MVP.
    [Table("tblPersons")]
    public class Person
    {
        [Key] public int PersonID { get; set; }
        public Guid PersonGuid { get; set; } = Guid.NewGuid();
        [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string PositionName { get; set; } = "دندانپزشک";
        [MaxLength(30)] public string? MedicalCouncilCode { get; set; }
        [MaxLength(20)] public string? Mobile { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}