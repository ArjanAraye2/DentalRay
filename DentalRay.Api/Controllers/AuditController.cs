using DentalRay.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/audit")]
    [Authorize(Policy = "AdminOnly")]
    public class AuditController : ControllerBase
    {
        private readonly DentalRayDbContext _context;

        public AuditController(
            DentalRayDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            string? search = null,
            int take = 250)
        {
            take =
                Math.Clamp(
                    take,
                    1,
                    1000);

            var query =
                _context.AuditLogs
                    .AsNoTracking()
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string text =
                    search.Trim();

                query =
                    query.Where(log =>
                        (log.UserName != null &&
                         log.UserName.Contains(text)) ||
                        log.Action.Contains(text) ||
                        (log.Path != null &&
                         log.Path.Contains(text)) ||
                        (log.Details != null &&
                         log.Details.Contains(text)));
            }

            var logs =
                await query
                    .OrderByDescending(
                        log =>
                            log.AuditLogID)
                    .Take(take)
                    .ToListAsync();

            return Ok(new
            {
                logs
            });
        }

        [HttpDelete("{auditLogID:long}")]
        public async Task<IActionResult> DeleteLog(
            long auditLogID)
        {
            var log =
                await _context.AuditLogs
                    .FirstOrDefaultAsync(
                        item =>
                            item.AuditLogID ==
                            auditLogID);

            if (log == null)
            {
                return NotFound(new
                {
                    message =
                        "لاگ پیدا نشد."
                });
            }

            _context.AuditLogs.Remove(log);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true
            });
        }

        [HttpDelete("cleanup")]
        public async Task<IActionResult> Cleanup(
            int olderThanDays = 90)
        {
            if (olderThanDays < 7 ||
                olderThanDays > 3650)
            {
                return BadRequest(new
                {
                    message =
                        "بازه نگهداری لاگ باید بین ۷ تا ۳۶۵۰ روز باشد."
                });
            }

            DateTime cutoff =
                DateTime.Now.AddDays(
                    -olderThanDays);

            int deleted =
                await _context.AuditLogs
                    .Where(
                        log =>
                            log.CreatedDate < cutoff)
                    .ExecuteDeleteAsync();

            return Ok(new
            {
                success = true,
                deleted
            });
        }
    }
}
