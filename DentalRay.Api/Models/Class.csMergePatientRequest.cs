namespace DentalRay.Api.Models
{
    // این کلاس اطلاعات لازم برای Merge دو بیمار را نگهداری می‌کند.
    //
    // Source = بیمار مبدأ
    // یعنی رکوردی که اشتباه یا تکراری است.
    //
    // Target = بیمار مقصد
    // یعنی رکورد صحیحی که قرار است اطلاعات مربوط به
    // بیمار مبدأ به آن منتقل شود.
    public class MergePatientRequest
    {
        // شناسه داخلی بیمار مبدأ
        public int SourcePatientID { get; set; }

        // شناسه داخلی بیمار مقصد
        public int TargetPatientID { get; set; }
    }
}