using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models;

/// <summary>One billable action performed as part of a radiology Study.</summary>
[Table("tblStudyActions")]
public class StudyAction
{
    [Key]
    public long StudyActionID { get; set; }
    public int StudyID { get; set; }
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
