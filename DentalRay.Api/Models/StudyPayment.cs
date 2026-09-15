using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // پرداخت متعلق به کل Study است، نه یک اقدام خاص.
    [Table("tblStudyPayments")]
    public class StudyPayment
    {
        [Key] public int StudyPaymentID { get; set; }
        public int StudyID { get; set; }
        [Column(TypeName = "decimal(18,0)")] public decimal Amount { get; set; }
        // 1=POS, 2=CardToCard, 3=Cash
        public byte PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        [MaxLength(100)] public string? ReferenceNumber { get; set; }
        [MaxLength(1000)] public string? Description { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}
