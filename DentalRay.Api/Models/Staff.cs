using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    [Table("tblStaff")]
    public class Staff
    {
        [Key]
        public int StaffID { get; set; }

        [Required, MaxLength(10)]
        public string NationalCode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        // 1 = Employee, 2 = Dentist
        public byte StaffType { get; set; }
        public int? SpecialtyID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
