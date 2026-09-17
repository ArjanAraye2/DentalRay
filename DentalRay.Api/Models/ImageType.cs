using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // Lookup table for radiology/image categories.
    // Values are seeded by the installation database script and maintained by SuperAdmin.
    [Table("tblImageTypes")]
    public class ImageType
    {
        [Key]
        public int ImageTypeID { get; set; }

        [Required, MaxLength(150)]
        public string ImageTypeName { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
