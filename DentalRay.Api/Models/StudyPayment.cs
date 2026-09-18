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
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
