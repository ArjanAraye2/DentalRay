using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // ============================================================
    // StudyShareLink
    // ============================================================
    // A single "view these images" link handed to a patient or a dentist.
    //
    // The token is a random secret stored here; it is never derived from the
    // patient data, so a link cannot be guessed or walked. The message that
    // carried it is kept for audit, and each opening is counted so the clinic
    // can see whether the link was actually used.
    // ============================================================
    [Table("tblStudyShareLinks")]
    public class StudyShareLink
    {
        [Key]
        public long ShareID { get; set; }

        public int StudyID { get; set; }

        [Required]
        [MaxLength(64)]
        public string Token { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? RecipientName { get; set; }

        [MaxLength(30)]
        public string? Mobile { get; set; }

        [MaxLength(1000)]
        public string? Message { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        // Null means "no expiry" - the clinic decides per link.
        public DateTime? ExpiresDate { get; set; }

        public DateTime? LastViewedAt { get; set; }

        public int ViewCount { get; set; }
    }
}
