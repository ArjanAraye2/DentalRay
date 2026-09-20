using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    /// <summary>
    /// How the clinic reached the patient, and how it went.
    ///
    /// SMS, a phone call and a note at reception all live here, so the patient
    /// record can show one history instead of only messages.
    /// </summary>
    [Table("tblPatientMessages")]
    public class PatientMessage
    {
        /// <summary>1 = SMS, 2 = phone call, 3 = in person, 4 = other.</summary>
        public byte Channel { get; set; } = PatientContactChannel.Sms;

        /// <summary>
        /// How the contact ended. Mostly used for calls:
        /// 1 = answered, 2 = no answer, 3 = left a message,
        /// 4 = asked for an SMS, 5 = appointment booked, 6 = will call back.
        /// </summary>
        public byte? Outcome { get; set; }

        /// <summary>
        /// Who made the contact, kept as a name because the built-in SuperAdmin
        /// account has no matching staff record.
        /// </summary>
        [MaxLength(150)]
        public string? ContactedByName { get; set; }

        /// <summary>Only meaningful for a phone call.</summary>
        public int? DurationMinutes { get; set; }

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
