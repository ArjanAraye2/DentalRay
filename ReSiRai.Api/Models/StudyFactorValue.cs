using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models;

/// <summary>
/// One recorded value of one factor in one visit. Rows are kept over time so the
/// AI sees the direction of the patient, not just a single moment, and every row
/// says where its number came from.
/// </summary>
[Table("tblStudyFactorValues")]
public class StudyFactorValue
{
    [Key]
    public long FactorValueID { get; set; }

    public int StudyID { get; set; }
    public int FactorID { get; set; }

    public decimal? ValueNumber { get; set; }
    [MaxLength(500)]
    public string? ValueText { get; set; }
    public bool? ValueBit { get; set; }
    public DateTime? ValueDate { get; set; }

    /// <summary>When the measurement was taken (may differ from the recording time).</summary>
    public DateTime ObservedAt { get; set; }

    /// <summary>
    /// The reference range printed on the patient's lab sheet, exactly as printed.
    /// It belongs to this observation and outranks the dictionary's default range.
    /// </summary>
    [MaxLength(500)]
    public string? RefText { get; set; }

    /// <summary>The unit printed on the lab sheet (may differ from the dictionary).</summary>
    [MaxLength(100)]
    public string? UnitText { get; set; }

    /// <summary>1 = manual, 2 = lab-report extraction, 3 = device, 4 = computed.</summary>
    public byte Source { get; set; }

    /// <summary>Extraction batch this value came from, when Source = 2.</summary>
    public long? ExtractionID { get; set; }

    /// <summary>Extraction confidence (0-100) for AI-captured values.</summary>
    public decimal? Confidence { get; set; }

    public int? CreatedByUserID { get; set; }
    public DateTime CreatedDate { get; set; }
}
