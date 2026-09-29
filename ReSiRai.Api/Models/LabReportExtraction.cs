using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models;

/// <summary>
/// One AI extraction run over one lab-report image (or page). The raw model output
/// is kept next to the human confirmation so every extracted number stays auditable.
/// </summary>
[Table("tblLabReportExtractions")]
public class LabReportExtraction
{
    [Key]
    public long ExtractionID { get; set; }

    public int StudyID { get; set; }

    [MaxLength(300)]
    public string? FileName { get; set; }
    [MaxLength(500)]
    public string? ImagePath { get; set; }
    [MaxLength(200)]
    public string? LabName { get; set; }

    /// <summary>Sampling date printed on the report; feeds the trend line.</summary>
    public DateTime? SampleDate { get; set; }

    /// <summary>Verbatim model output, kept as evidence of what was extracted.</summary>
    public string? RawJson { get; set; }

    /// <summary>1 = pending review, 2 = confirmed, 3 = partial, 4 = failed.</summary>
    public byte Status { get; set; } = 1;

    public DateTime CreatedDate { get; set; }
    public int? ConfirmedByUserID { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
