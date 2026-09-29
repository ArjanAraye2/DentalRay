using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models
{
    [Table("tblClinicStaff")]
    public class ClinicStaff
    {
        public int ClinicID { get; set; }
        public int StaffID { get; set; }
    }
}
