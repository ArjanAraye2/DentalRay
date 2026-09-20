using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    /// <summary>
    /// A card-reader (POS) terminal Dentix can send a payment amount to.
    ///
    /// Terminals differ by vendor, so the connection details and the bytes sent
    /// to the device are configuration rather than code. A new model can usually
    /// be supported by filling a form instead of shipping a new build.
    /// </summary>
    [Table("tblPosSettings")]
    public class PosSetting
    {
        [Key]
        public int PosSettingID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>1 = TCP/IP, 2 = serial (COM), 3 = USB / PC-POS, 4 = cloud service.</summary>
        public byte ConnectionType { get; set; }

        [MaxLength(100)]
        public string? Host { get; set; }

        public int? Port { get; set; }

        [MaxLength(20)]
        public string? ComPort { get; set; }

        public int? BaudRate { get; set; }

        /// <summary>Protocol implementation name; "Generic" ships with Dentix.</summary>
        [Required, MaxLength(50)]
        public string Protocol { get; set; } = "Generic";

        /// <summary>Text sent to the device. Placeholders: {amount} {currency} {invoice}.</summary>
        [MaxLength(500)]
        public string? RequestPattern { get; set; }

        /// <summary>Text that marks a successful answer from the device.</summary>
        [MaxLength(200)]
        public string? SuccessPattern { get; set; }

        [Required, MaxLength(20)]
        public string Encoding { get; set; } = "UTF8";

        public int TimeoutSeconds { get; set; } = 5;

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; }

        public DateTime? LastTestAt { get; set; }

        [MaxLength(500)]
        public string? LastTestMessage { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }
    }
}
