using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ReSiRai.Api.Controllers;

// کنترلرِ پشتیبان: وضعیت، اجرای دستی و تنظیمات (مسیر و تعداد نسخه).
// همه فقط برای مدیرِ سیستم؛ برای دیگران این آدرس‌ها اصلاً وجود ندارند.
[ApiController]
[Route("api/backup")]
public sealed class BackupController : ControllerBase
{
    private readonly BackupService _backup;
    private readonly AppEventLogger _events;

    public BackupController(BackupService backup, AppEventLogger events)
    {
        _backup = backup;
        _events = events;
    }

    public sealed record SettingsRequest(string? RootPath, int KeepBackups);

    private bool Allowed => StudyAccessService.IsSuperAdmin(User);

    [HttpGet("status")]
    public IActionResult Status()
    {
        if (!Allowed) return NotFound(new { success = false, message = "پیدا نشد." });
        return Ok(new { success = true, status = _backup.GetStatus() });
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        if (!Allowed) return NotFound(new { success = false, message = "پیدا نشد." });
        var (ok, error) = await _backup.RunAsync(cancellationToken);
        if (!ok)
            return StatusCode(500, new { success = false, message = error ?? "پشتیبان انجام نشد.", status = _backup.GetStatus() });
        return Ok(new { success = true, status = _backup.GetStatus() });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> Settings([FromBody] SettingsRequest request, CancellationToken cancellationToken)
    {
        if (!Allowed) return NotFound(new { success = false, message = "پیدا نشد." });
        try
        {
            _backup.UpdateSettings(request?.RootPath, request?.KeepBackups ?? 7);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return StatusCode(500, new { success = false, message = e.Message });
        }

        await _events.LogAsync("backup",
            detail: $"تنظیماتِ پشتیبان تغییر کرد ← {(request?.RootPath ?? "").Trim()} (نگه‌داری {Math.Clamp(request?.KeepBackups ?? 7, 1, 100)} نسخه)",
            userID: CurrentUserID());

        return Ok(new { success = true, status = _backup.GetStatus() });
    }

    public sealed record RestoreRequest(string? File, string? Confirm);

    private int? CurrentUserID()
        => int.TryParse(User.FindFirst("UserID")?.Value, out int uid) && uid > 0 ? uid : (int?)null;

    private static string NormalizeWord(string? value) =>
        (value ?? string.Empty).Replace('\u064A', 'ی').Replace('\u0643', 'ک').Replace("\u200C", "").Trim();

    [HttpGet("restore-points")]
    public IActionResult RestorePoints()
    {
        if (!Allowed) return NotFound(new { success = false, message = "پیدا نشد." });
        return Ok(new { success = true, files = _backup.GetRestorePoints() });
    }

    /// <summary>
    /// جایگزینیِ دیتابیسِ فعلی با یکی از نسخه‌های پشتیبان. این کار برگشت‌پذیر نیست،
    /// پس عبارتِ «بازگردانی» باید تایپ شود و خودِ سرور هم آن را بررسی می‌کند.
    /// </summary>
    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreRequest? request, CancellationToken cancellationToken)
    {
        if (!Allowed) return NotFound(new { success = false, message = "پیدا نشد." });
        // همان مقایسهٔ رابطِ کاربری: ي/ك و نیم‌فاصله نباید مانعِ تأیید شوند.
        string confirm = NormalizeWord(request?.Confirm);
        if (!string.Equals(confirm, "بازگردانی", StringComparison.Ordinal))
            return BadRequest(new
            {
                success = false,
                needsConfirm = true,
                message = "برای بازگردانی باید عبارت «بازگردانی» را دقیقاً تایپ کنید."
            });

        string fileName = Path.GetFileName(request?.File ?? string.Empty);
        try
        {
            await _backup.RestoreDatabaseAsync(request?.File, cancellationToken);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { success = false, message = e.Message });
        }
        catch (FileNotFoundException e)
        {
            return NotFound(new { success = false, message = e.Message });
        }
        catch (Microsoft.Data.SqlClient.SqlException e)
        {
            string detail = e.Message.Length > 300 ? e.Message[..300] : e.Message;
            await _events.LogAsync("backup", outcome: "fail",
                detail: $"بازگردانیِ دیتابیس ناموفق ({fileName}) — {detail}", userID: CurrentUserID());
            return StatusCode(500, new { success = false, message = $"بازگردانی انجام نشد: {detail}" });
        }

        await _events.LogAsync("backup", detail: $"بازگردانیِ دیتابیس از {fileName}", userID: CurrentUserID());
        // دیتابیسِ برگشته ممکن است قدیمی‌تر از ساختارِ فعلی باشد؛ ساختار در شروعِ
        // بعدیِ برنامه ترمیم می‌شود، پس کاربر باید برنامه را یک‌بار ببندد و باز کند.
        return Ok(new { success = true, restartRequired = true, status = _backup.GetStatus() });
    }

    [HttpPost("restore-images")]
    public async Task<IActionResult> RestoreImages(CancellationToken cancellationToken)
    {
        if (!Allowed) return NotFound(new { success = false, message = "پیدا نشد." });
        try
        {
            await _backup.RestoreImagesAsync(cancellationToken);
        }
        catch (InvalidOperationException e)
        {
            return StatusCode(500, new { success = false, message = e.Message });
        }

        await _events.LogAsync("backup", detail: "تصاویرِ غایب از پشتیبان کپی شد", userID: CurrentUserID());
        return Ok(new { success = true, message = "تصاویرِ غایب از پشتیبان کپی شد؛ فایل‌های موجود دست نخوردند." });
    }
}
