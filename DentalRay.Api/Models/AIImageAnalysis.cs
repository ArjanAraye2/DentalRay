using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models;

/// <summary>
/// One stored AI result for one image. It exists so a picture of a patient is sent
/// to the outside service once instead of on every click, and so repeat views cost
/// nothing.
/// </summary>
[Table("tblAIImageAnalyses")]
public class AIImageAnalysis
{
    [Key]
    public long AIImageAnalysisID { get; set; }
    public long ImageID { get; set; }
    /// <summary>1 = radiology analysis, 2 = legacy card extraction (reserved).</summary>
    public byte Kind { get; set; }
    /// <summary>The model that produced the result, so a changed model can be noticed.</summary>
    [MaxLength(120)]
    public string Model { get; set; } = string.Empty;
    /// <summary>The prompt revision behind the result; a newer prompt means an older result.</summary>
    public int PromptVersion { get; set; }
    public string AnalysisJson { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public int? AnalyzedByUserID { get; set; }
}
