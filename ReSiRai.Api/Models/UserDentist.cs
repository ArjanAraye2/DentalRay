using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models
{
    // Defines which dentist an employee User may work with inside a Clinic.
    // The SQL table has a composite primary key: UserID + ClinicID + DentistStaffID.
    [Table("tblUserDentists")]
    public class UserDentist
    {
        public int UserID { get; set; }
        public int ClinicID { get; set; }
        public int DentistStaffID { get; set; }
    }
}
