namespace DentalRay.Api.Models
{
    // Data sent by the Frontend when an existing Study is edited.
    // PatientID is intentionally absent: a Study cannot be moved to another
    // patient through ordinary editing; patient transfer is handled by Merge.
    public class UpdateRadiologyStudyRequest
    {
        public DateTime StudyDate { get; set; }

        // Foreign key to tblStudyTypes. The Backend verifies that the selected
        // type exists and is active before updating the Study.
        public int StudyTypeID { get; set; }

        public string? BodyPart { get; set; }
        public string? Description { get; set; }
        public string? Report { get; set; }

        // FDI numbers selected in the odontogram. Empty means no teeth selected.
        public List<int> ToothNumbers { get; set; } = new();
    }
}
