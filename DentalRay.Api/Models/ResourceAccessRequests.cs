namespace DentalRay.Api.Models
{
    public sealed class ResourceVisibilityRequest
    {
        // 0 = Private، 1 = Public برای کاربران واردشده به همین برنامه
        public byte Visibility { get; set; }
    }

    public sealed class CreateResourceGrantRequest
    {
        // 1 = Study، 2 = Image
        public byte ResourceType { get; set; }
        public long ResourceID { get; set; }
        public int RecipientUserID { get; set; }
    }

    public sealed class AttachImageToStudyRequest
    {
        public int StudyID { get; set; }
    }
}
