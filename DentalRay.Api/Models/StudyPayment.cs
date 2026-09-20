using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models;

/// <summary>One payment received for a radiology Study.</summary>
[Table("tblStudyPayments")]
public class StudyPayment
{
    [Key]
    public long StudyPaymentID { get; set; }
    public int StudyID { get; set; }
    public DateTime PaymentDate { get; set; }
    // 1=POS, 2=card-to-card transfer, 3=cash. Null preserves older records whose method is unknown.
    public byte? PaymentMethod { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// True when this row is money paid back to the patient. Refunds are stored as
    /// a positive amount flagged here rather than a negative amount, so the
    /// Amount > 0 constraint stays valid and totals stay unambiguous.
    /// </summary>
    public bool IsRefund { get; set; }

    /// <summary>Terminal used for the last dispatch attempt, if any.</summary>
    public int? PosSettingID { get; set; }

    public DateTime? PosSentAt { get; set; }

    public bool? PosSuccess { get; set; }

    [MaxLength(500)]
    public string? PosMessage { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
