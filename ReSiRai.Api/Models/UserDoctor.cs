using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models
{
    // Defines which doctor an employee User may work with inside a Clinic.
    // The SQL table has a composite primary key: UserID + ClinicID + DoctorStaffID.
    [Table("tblUserDoctors")]
    public class UserDoctor
    {
        public int UserID { get; set; }
        public int ClinicID { get; set; }
        public int DoctorStaffID { get; set; }
    }
}
