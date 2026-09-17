using System.ComponentModel.DataAnnotations;

namespace DentalRay.Api.Models
{
    // Data sent by the Frontend when an existing Study is edited.
    // PatientID is intentionally absent: a Study cannot be moved to another
    // patient through ordinary editing; patient transfer is handled by Merge.
    public class UpdateRadiologyStudyRequest
    {
        public DateTime StudyDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string StudyType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? BodyPart { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public string? Report { get; set; }

        // FDI numbers selected in the odontogram. Empty means no teeth selected.
        public List<int> ToothNumbers { get; set; } = new();
    }
}