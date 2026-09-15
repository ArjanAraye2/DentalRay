using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    [Table("tblOrganizationMembers")]
    public class OrganizationMember
    {
        [Key] public int OrganizationMemberID { get; set; }
        public int OrganizationID { get; set; }
        public int PersonID { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}