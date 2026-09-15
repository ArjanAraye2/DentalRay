using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalRay.Api.Models
{
    // این کلاس نماینده یک فایل تصویر رادیولوژی
    // در برنامه DentalRay است.
    //
    // این کلاس به جدول:
    //
    // tblRadiologyImages
    //
    // در دیتابیس SQL Server متصل خواهد شد.
    [Table("tblRadiologyImages")]
    public class RadiologyImage
    {
        // --------------------------------------------------------
        // ImageID
        // --------------------------------------------------------
        //
        // کلید اصلی جدول tblRadiologyImages است.
        //
        // مقدار آن توسط SQL Server و به صورت Identity
        // تولید می‌شود.
        //
        // در دیتابیس نوع این ستون BIGINT است،
        // بنابراین در C# از long استفاده می‌کنیم.
        [Key]
        public long ImageID { get; set; }


        // --------------------------------------------------------
        // StudyID
        // --------------------------------------------------------
        //
        // مشخص می‌کند این تصویر متعلق به کدام
        // RadiologyStudy است.
        //
        // این مقدار به StudyID موجود در
        // tblRadiologyStudies اشاره می‌کند.
        //
        // بنابراین رابطه به این صورت است:
        //
        // tblRadiologyStudies
        //          1
        //          |
        //          N
        // tblRadiologyImages
        public int StudyID { get; set; }


        // --------------------------------------------------------
        // FileName
        // --------------------------------------------------------
        //
        // نام فایل تصویر است.
        //
        // مثال:
        //
        // 8A31F2.jpg
        //
        // یا:
        //
        // pano_001.png
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;


        // --------------------------------------------------------
        // RelativePath
        // --------------------------------------------------------
        //
        // مسیر نسبی فایل تصویر است.
        //
        // نکته بسیار مهم:
        //
        // ما مسیر کامل فایل را در دیتابیس ذخیره نمی‌کنیم.
        //
        // مثال مسیر فیزیکی:
        //
        // D:\RadiologyData\7\1032\8A31F2.jpg
        //
        // چیزی که در دیتابیس ذخیره می‌شود:
        //
        // 7\1032\8A31F2.jpg
        //
        // مسیر اصلی RadiologyData بعداً از Configuration
        // خوانده خواهد شد.
        //
        // به این ترتیب اگر محل اصلی ذخیره تصاویر تغییر کند،
        // لازم نیست اطلاعات دیتابیس را تغییر دهیم.
        [Required]
        [MaxLength(1000)]
        public string RelativePath { get; set; } = string.Empty;


        // --------------------------------------------------------
        // ContentType
        // --------------------------------------------------------
        //
        // نوع فایل تصویر را مشخص می‌کند.
        //
        // مثال:
        //
        // image/jpeg
        // image/png
        //
        // در نسخه فعلی DentalRay فقط JPG و PNG
        // موردنظر ما هستند.
        [Required]
        [MaxLength(50)]
        public string ContentType { get; set; } = string.Empty;


        // --------------------------------------------------------
        // Description
        // --------------------------------------------------------
        // توضیح ایجادکننده تصویر؛ مستقل از توضیحات Study.
        [MaxLength(1000)]
        public string? Description { get; set; }


        // --------------------------------------------------------
        // CreatedDate
        // --------------------------------------------------------
        //
        // تاریخ و ساعت ثبت این تصویر در سیستم.
        //
        // از این فیلد برای مرتب‌سازی اولیه تصاویر
        // نیز می‌توان استفاده کرد.
        public DateTime CreatedDate { get; set; }
    }
}