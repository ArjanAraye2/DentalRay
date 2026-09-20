using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    /// <summary>One message sent to a patient, kept so the clinic can prove what went out.</summary>
    [Table("tblPatientMessages")]
    public class PatientMessage
    {
        [Key]
        public long MessageID { get; set; }

        public int PatientID { get; set; }

        [Required, MaxLength(30)]
        public string Mobile { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Body { get; set; } = string.Empty;

        /// <summary>Template name, or null when the text was typed freely.</summary>
        [MaxLength(50)]
        public string? TemplateKey { get; set; }

        /// <summary>0 = queued, 1 = sent, 2 = failed.</summary>
        public byte Status { get; set; }

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        /// <summary>Provider message id, kept for a future delivery check.</summary>
        [MaxLength(100)]
        public string? ProviderKey { get; set; }

        public int? AppointmentID { get; set; }

        public DateTime? SentAt { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }
    }

    /// <summary>A scheduled visit, used for reminders.</summary>
    [Table("tblAppointments")]
    public class Appointment
    {
        [Key]
        public int AppointmentID { get; set; }

        public int PatientID { get; set; }

        public DateTime AppointmentDate { get; set; }

        public int? DentistStaffID { get; set; }

        /// <summary>1 = scheduled, 2 = attended, 3 = cancelled, 4 = no-show.</summary>
        public byte Status { get; set; } = 1;

        [MaxLength(500)]
        public string? Note { get; set; }

        /// <summary>Set once the reminder has gone out, so it is never sent twice.</summary>
        public DateTime? ReminderSentAt { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }
    }
}
