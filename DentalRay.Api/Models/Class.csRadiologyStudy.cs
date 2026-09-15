using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // این کلاس نماینده یک پرونده/مطالعه رادیولوژی
    // مربوط به یک بیمار است.
    //
    // این کلاس به جدول:
    //
    // tblRadiologyStudies
    //
    // در دیتابیس DentalRay متصل خواهد شد.
    [Table("tblRadiologyStudies")]
    public class RadiologyStudy
    {
        // --------------------------------------------------------
        // StudyID
        // --------------------------------------------------------
        //
        // کلید اصلی جدول tblRadiologyStudies است.
        //
        // مقدار آن توسط SQL Server و به صورت Identity
        // تولید می‌شود.
        [Key]
        public int StudyID { get; set; }


        // --------------------------------------------------------
        // PatientID
        // --------------------------------------------------------
        //
        // مشخص می‌کند این مطالعه رادیولوژی متعلق به کدام بیمار است.
        //
        // این مقدار همان PatientID موجود در tblPatients است.
        //
        // بنابراین رابطه بین دو جدول به صورت زیر است:
        //
        // tblPatients
        //      1
        //      |
        //      N
        // tblRadiologyStudies
        //
        // در عملیات Merge نیز دقیقاً همین مقدار است که
        // از SourcePatientID به TargetPatientID تغییر خواهد کرد.
        public int PatientID { get; set; }

        // مطب/مرکزی که Study در آن ثبت شده است.
        public int? OrganizationID { get; set; }

        // دندانپزشک مسئول Study. برای سازگاری با اطلاعات قدیمی Nullable است.
        public int? DentistPersonID { get; set; }

        // مالک Study همان کاربری است که آن را ایجاد کرده است.
        // هنگام ایجاد، مقدار ارسالی Client نادیده گرفته می‌شود.
        public int? OwnerUserID { get; set; }

        // 0 = Private، 1 = Public برای کاربران احرازشدهٔ همین برنامه.
        public byte Visibility { get; set; } = 0;

        // تخفیف Study: صفر=بدون تخفیف، 1=مبلغ ثابت، 2=درصدی.
        public byte DiscountType { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } = 0;

        // مبلغ نهایی تخفیف در لحظه محاسبه ذخیره می‌شود تا سوابق مالی ثابت بمانند.
        [Column(TypeName = "decimal(18,0)")]
        public decimal DiscountAmount { get; set; } = 0;


        // --------------------------------------------------------
        // StudyDate
        // --------------------------------------------------------
        //
        // تاریخ و ساعت انجام مطالعه رادیولوژی.
        //
        // در SQL Server به صورت DATETIME2 ذخیره می‌شود.
        //
        // تبدیل به تاریخ شمسی برای نمایش به کاربر،
        // در لایه Frontend انجام خواهد شد.
        public DateTime StudyDate { get; set; }


        // --------------------------------------------------------
        // StudyType
        // --------------------------------------------------------
        //
        // نوع مطالعه رادیولوژی.
        //
        // مثال:
        // Panoramic
        // Periapical
        // Cephalometric
        // و ...
        [Required]
        [MaxLength(50)]
        public string StudyType { get; set; } = string.Empty;


        // --------------------------------------------------------
        // BodyPart
        // --------------------------------------------------------
        //
        // قسمت مورد بررسی.
        //
        // این فیلد اختیاری است.
        [MaxLength(100)]
        public string? BodyPart { get; set; }


        // --------------------------------------------------------
        // Description
        // --------------------------------------------------------
        //
        // توضیحات مربوط به مطالعه.
        //
        // این فیلد اختیاری است.
        [MaxLength(1000)]
        public string? Description { get; set; }


        // --------------------------------------------------------
        // Report
        // --------------------------------------------------------
        //
        // متن گزارش رادیولوژی.
        //
        // در دیتابیس نوع این ستون NVARCHAR(MAX) است.
        //
        // بنابراین در C# محدودیت طول مشخصی برای آن تعیین
        // نمی‌کنیم.
        public string? Report { get; set; }


        // --------------------------------------------------------
        // CreatedDate
        // --------------------------------------------------------
        //
        // تاریخ و ساعت ایجاد این رکورد در سیستم.
        public DateTime CreatedDate { get; set; }


        // --------------------------------------------------------
        // ModifiedDate
        // --------------------------------------------------------
        //
        // آخرین تاریخ ویرایش رکورد.
        //
        // چون ممکن است رکورد هنوز ویرایش نشده باشد،
        // این مقدار می‌تواند NULL باشد.
        public DateTime? ModifiedDate { get; set; }
    }
}
