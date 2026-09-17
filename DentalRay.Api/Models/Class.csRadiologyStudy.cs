using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    [Table("tblRadiologyStudies")]
    public class RadiologyStudy
    {
        [Key]
        public int StudyID { get; set; }
        public int PatientID { get; set; }
        public DateTime StudyDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string StudyType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? BodyPart { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public string? Report { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // UI/API helper only. Tooth selections are physically stored in
        // tblRadiologyStudyTeeth, not in tblRadiologyStudies.
        [NotMapped]
        public List<int> ToothNumbers { get; set; } = new();
    }
}