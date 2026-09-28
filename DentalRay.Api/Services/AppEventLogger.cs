using DentalRay.Api.Data;
using DentalRay.Api.Models;

namespace DentalRay.Api.Services;

/// <summary>
/// ثبتِ رویدادهای کاربردی سامانه برای پیگیری در مطب.
///
/// یک قاعدهٔ سخت: لاگ هرگز نباید کارِ اصلی را متوقف کند. به همین دلیل هر خطایی
/// (دیتابیس قطع، ستون ناموجود، انصرافِ درخواست) بلعیده می‌شود و فقط در لاگِ کنسول
/// می‌ماند. ثبت هم با CancellationToken.None انجام می‌شود تا اگر کاربر در همان
/// لحظه پنجره را بست، رویداد از بین نرود.
/// </summary>
public sealed class AppEventLogger
{
    private readonly DentalRayDbContext _db;

    public AppEventLogger(DentalRayDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string kind, string outcome = "ok", string detail = "",
        int? durationMs = null, int? userID = null, string? userName = null)
    {
        try
        {
            _db.AppEvents.Add(new AppEvent
            {
                EventAt = DateTime.Now,
                Kind = Cut(kind, 40) ?? string.Empty,
                Outcome = Cut(outcome, 20) ?? string.Empty,
                Detail = Cut(detail, 500) ?? string.Empty,
                UserID = userID,
                UserName = userName is null ? null : Cut(userName, 64),
                DurationMs = durationMs
            });
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DentalRay رویداد] ثبت نشد ({kind}): {e.GetType().Name}: {e.Message}");
        }
    }

    private static string? Cut(string? value, int max)
    {
        if (value is null) return null;
        if (value.Length <= max) return value;
        return value[..max];
    }
}
