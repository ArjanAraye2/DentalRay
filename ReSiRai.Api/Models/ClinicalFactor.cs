using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models;

/// <summary>
/// One entry of the global clinical factor dictionary. Every factor carries its
/// scientific identity (international code, standard unit, reference range and the
/// guideline behind it) so numbers sent to the AI are always traceable to a source.
/// </summary>
[Table("tblClinicalFactors")]
public class ClinicalFactor
{
    [Key]
    public int FactorID { get; set; }

    /// <summary>Stable internal code, e.g. LAB.HBA1C or VITAL.SBP.</summary>
    [MaxLength(60)]
    public string FactorCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string NameFa { get; set; } = string.Empty;

    [MaxLength(200)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Short lab-sheet abbreviation (Hb, WBC, Cr) shown next to the Persian name.</summary>
    [MaxLength(20)]
    public string? ShortCode { get; set; }

    /// <summary>Vitals | Anthropometry | History | Exam | Lab | Imaging | Score.</summary>
    [MaxLength(40)]
    public string Category { get; set; } = string.Empty;

    /// <summary>1 = number, 2 = enum, 3 = boolean, 4 = text, 5 = date.</summary>
    public byte DataType { get; set; }

    /// <summary>Unit in UCUM form; a value is never stored without its unit.</summary>
    [MaxLength(30)]
    public string? UnitUCUM { get; set; }

    /// <summary>LOINC code of the test; null while a specialist has not confirmed one.</summary>
    [MaxLength(20)]
    public string? LoincCode { get; set; }

    /// <summary>0 = pending specialist review, 1 = verified by a specialist.</summary>
    public byte LoincStatus { get; set; }

    public decimal? RefLow { get; set; }
    public decimal? RefHigh { get; set; }

    /// <summary>Reference text when a numeric range is not the right shape (targets, sex splits).</summary>
    [MaxLength(200)]
    public string? RefText { get; set; }

    /// <summary>Guideline the range comes from, e.g. ADA Standards of Care 2024.</summary>
    [MaxLength(200)]
    public string RefSource { get; set; } = string.Empty;

    /// <summary>Population the range applies to, e.g. adult / pregnant.</summary>
    [MaxLength(120)]
    public string? RefPopulation { get; set; }

    /// <summary>1 = high is abnormal, 2 = low is abnormal, 3 = both sides.</summary>
    public byte? AbnormalDirection { get; set; }

    /// <summary>JSON list of options for enum factors.</summary>
    public string? OptionsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }
}
