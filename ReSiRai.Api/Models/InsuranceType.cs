using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models;

/// <summary>
/// One entry of the insurance dictionary. A single table holds both kinds so a
/// new company (a bank, a steel company, ...) is just a row, separated by the
/// IsSupplementary flag. Only the SuperAdmin maintains this list.
/// </summary>
[Table("tblInsuranceTypes")]
public class InsuranceType
{
    [Key]
    public int InsuranceTypeID { get; set; }

    [Required]
    [MaxLength(100)]
    public string InsuranceTypeName { get; set; } = string.Empty;

    /// <summary>false = بیمهٔ پایه, true = بیمهٔ تکمیلی.</summary>
    public bool IsSupplementary { get; set; }

    /// <summary>Soft delete: a type used by any patient is deactivated, never removed.</summary>
    public bool IsActive { get; set; } = true;
}
