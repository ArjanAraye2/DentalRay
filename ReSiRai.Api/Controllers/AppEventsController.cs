using ReSiRai.Api.Data;
using ReSiRai.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ReSiRai.Api.Controllers;

// لاگِ رویدادها فقط برای مدیرِ سیستم است؛ برای دیگران این آدرس اصلاً وجود ندارد.
[ApiController]
[Route("api/appevents")]
public sealed class AppEventsController : ControllerBase
{
    private readonly ReSiRaiDbContext _db;

    public AppEventsController(ReSiRaiDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? kind, [FromQuery] string? outcome,
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? q,
        [FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        if (!StudyAccessService.IsSuperAdmin(User))
            return NotFound(new { success = false, message = "پیدا نشد." });

        var query = _db.AppEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(kind) && kind != "all")
            query = query.Where(x => x.Kind == kind);
        if (!string.IsNullOrWhiteSpace(outcome) && outcome != "all")
            query = query.Where(x => x.Outcome == outcome);
        // تاریخِ «تا» شاملِ خودِ روز می‌شود.
        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(x => x.EventAt >= fromDate.Date);
        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(x => x.EventAt < toDate.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q))
        {
            string needle = q.Trim();
            query = query.Where(x => x.Detail.Contains(needle) ||
                (x.UserName != null && x.UserName.Contains(needle)));
        }

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.EventAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => new
            {
                x.EventID,
                x.EventAt,
                x.Kind,
                x.Outcome,
                x.Detail,
                x.UserID,
                x.UserName,
                x.DurationMs
            })
            .ToListAsync(cancellationToken);

        var kinds = await _db.AppEvents.AsNoTracking()
            .Select(x => x.Kind)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, events = rows, kinds, total });
    }
}
