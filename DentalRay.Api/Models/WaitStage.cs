using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    /// <summary>
    /// What a patient is waiting for, per specialty.
    ///
    /// A radiology patient waits for an image, a prosthodontics patient waits for a
    /// prosthesis, an endodontics patient waits for the next session. The stage is
    /// therefore configuration rather than a fixed list, and may be scoped to a
    /// specialty (NULL means it applies to all).
    /// </summary>
    [Table("tblWaitStages")]
    public class WaitStage
    {
        [Key]
        public int WaitStageID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>NULL means the stage applies to every specialty.</summary>
        public int? SpecialtyID { get; set; }

        /// <summary>Message sent to the patient when this stage becomes ready.</summary>
        [MaxLength(500)]
        public string? SmsTemplate { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }
    }
}
