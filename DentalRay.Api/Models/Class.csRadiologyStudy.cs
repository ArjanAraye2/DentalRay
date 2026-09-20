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

        // These two fields are nullable only for legacy Studies created before
        // Clinic/Dentist support was introduced. New Studies will later require them.
        public int? ClinicID { get; set; }
        public int? DentistStaffID { get; set; }

        public DateTime StudyDate { get; set; }

        // StudyType is now a lookup value stored in tblStudyTypes.
        // The old free-text StudyType column has been removed from SQL Server.
        public int StudyTypeID { get; set; }

        [MaxLength(100)]
        public string? BodyPart { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public string? Report { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// 1 = open, 2 = completed, 3 = needs another study later.
        ///
        /// Studies used to carry no state, so the list could not show which
        /// patients still had work outstanding.
        /// </summary>
        public byte Status { get; set; } = 2;

        /// <summary>When the user wants to be reminded, used for status 3.</summary>
        public DateTime? FollowUpDate { get; set; }

        [MaxLength(500)]
        public string? FollowUpNote { get; set; }

        // UI/API helper only. Tooth selections are physically stored in
        // tblRadiologyStudyTeeth, not in tblRadiologyStudies.
        [NotMapped]
        public List<int> ToothNumbers { get; set; } = new();
    }
}
