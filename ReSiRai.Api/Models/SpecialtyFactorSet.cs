using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models;

/// <summary>
/// Which factors one specialty uses. Factors live in a single shared dictionary,
/// so a new specialty adds only its own factors and reuses the rest.
/// </summary>
[Table("tblSpecialtyFactorSets")]
public class SpecialtyFactorSet
{
    public int SpecialtyID { get; set; }
    public int FactorID { get; set; }

    /// <summary>The visit cannot be considered complete without this factor.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Always shown first, regardless of the reason for the visit.</summary>
    public bool IsCommon { get; set; }

    public int SortOrder { get; set; }
}
