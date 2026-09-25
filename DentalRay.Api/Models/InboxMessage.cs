using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    /// <summary>
    /// A phone that agreed to forward its SMS to Dentix.
    /// Pairing starts with a QR code shown by the clinic, so consent is
    /// recorded and the phone authenticates with its own random key.
    /// </summary>
    [Table("tblPairedDevices")]
    public class PairedDevice
    {
        [Key]
        public int DeviceID { get; set; }

        [Required]
        [MaxLength(64)]
        public string DeviceToken { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Label { get; set; } = string.Empty;

        // 1 = clinic phone, 2 = patient's phone.
        public byte OwnerKind { get; set; } = 1;

        public int? PatientID { get; set; }

        public DateTime PairedDate { get; set; }
        public DateTime? LastSeenDate { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// One SMS read from a paired phone, together with the matcher's verdict.
    /// Status 0 means "still waiting for the secretary" - nothing reaches a
    /// patient record until someone confirms an unsure match.
    /// </summary>
    [Table("tblInboxMessages")]
    public class InboxMessage
    {
        [Key]
        public long MessageID { get; set; }

        // NULL برای پیام‌هایی است که از مرورگر بیمار می‌آیند (بدون گوشی جفت‌شده)
        public int? DeviceID { get; set; }

        [MaxLength(30)]
        public string? SenderMobile { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Body { get; set; } = string.Empty;

        // Share links found in the text, newline separated.
        [MaxLength(2000)]
        public string? Links { get; set; }

        public DateTime ReceivedDate { get; set; }

        public int? PatientID { get; set; }

        // 1 = share link token, 2 = national code, 3 = sender mobile, 4 = name.
        public byte? MatchMethod { get; set; }

        // 0 = pending, 1 = linked, 2 = rejected.
        public byte Status { get; set; }

        public int? LinkedStudyID { get; set; }

        public int ImportedCount { get; set; }

        [MaxLength(300)]
        public string? Note { get; set; }

        // 1 = پیامک خوانده‌شده از گوشی جفت‌شده، 2 = متنی که بیمار در مرورگر چسبانده
        public byte Source { get; set; } = 1;

        public DateTime CreatedDate { get; set; }
    }
}
