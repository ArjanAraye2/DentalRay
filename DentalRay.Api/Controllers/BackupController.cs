using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DentalRay.Api.Controllers;

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
            userID: int.TryParse(User.FindFirst("UserID")?.Value, out int uid) && uid > 0 ? uid : (int?)null);

        return Ok(new { success = true, status = _backup.GetStatus() });
    }
}
