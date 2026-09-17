using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // ============================================================
    // RadiologyStudyTooth
    // ============================================================
    // Stores one tooth selected for one radiology Study.
    // ToothNumber uses the FDI two-digit numbering system.
    // Permanent: 11-18, 21-28, 31-38, 41-48
    // Primary:   51-55, 61-65, 71-75, 81-85
    // ============================================================
    [Table("tblRadiologyStudyTeeth")]
    public class RadiologyStudyTooth
    {
        public int StudyID { get; set; }

        public byte ToothNumber { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
