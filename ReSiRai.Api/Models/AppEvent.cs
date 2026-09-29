using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReSiRai.Api.Models;

/// <summary>
/// یک رویدادِ ثبت‌شدهٔ سامانه: ورود، ارسالِ پیامک، تحلیلِ AI، اجرای پشتیبان یا شروعِ برنامه.
///
/// فقط برایِ پیگیری است — هیچ تصمیمی بر اساسِ آن گرفته نمی‌شود، و ثبتش هرگز
/// نباید کارِ اصلی را کند یا خراب کند (ثبت در سرویسِ جدا و بلعیدنِ خطا).
/// </summary>
[Table("tblAppEvents")]
public class AppEvent
{
    [Key]
    public long EventID { get; set; }

    /// <summary>زمانِ محلیِ مطب، چون همان چیزی است که کاربر می‌بیند.</summary>
    public DateTime EventAt { get; set; }

    /// <summary>دستهٔ رویداد: login | sms | ai.image | ai.images | backup | app.start</summary>
    [MaxLength(40)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>ok یا fail — تا فیلترِ «ناموفق‌ها» در یک نگاه ممکن باشد.</summary>
    [MaxLength(20)]
    public string Outcome { get; set; } = "ok";

    [MaxLength(500)]
    public string Detail { get; set; } = string.Empty;

    public int? UserID { get; set; }

    [MaxLength(64)]
    public string? UserName { get; set; }

    /// <summary>مدتِ انجام به میلی‌ثانیه؛ برای تحلیلِ AI و پشتیبان معنادار است.</summary>
    public int? DurationMs { get; set; }
}
