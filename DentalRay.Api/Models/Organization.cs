using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // A clinic or radiology center using DentalRay.
    [Table("tblOrganizations")]
    public class Organization
    {
        [Key] public int OrganizationID { get; set; }
        public Guid OrganizationGuid { get; set; } = Guid.NewGuid();
        public byte OrganizationType { get; set; } = 1;
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(30)] public string? Phone { get; set; }
        [MaxLength(20)] public string? Mobile { get; set; }
        [MaxLength(500)] public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}