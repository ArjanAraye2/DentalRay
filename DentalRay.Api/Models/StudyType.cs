using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // Lookup table for the type of a radiology Study.
    // Examples: Panoramic, Periapical, Bitewing, CBCT, etc.
    [Table("tblStudyTypes")]
    public class StudyType
    {
        [Key]
        public int StudyTypeID { get; set; }

        [Required]
        [MaxLength(150)]
        public string StudyTypeName { get; set; } = string.Empty;

        // Old Studies may continue to reference an inactive type, but the
        // Frontend should normally offer only active types for new Studies.
        public bool IsActive { get; set; }
    }
}
