using System.Security.Claims;
using System.Text.Json;
using DentalRay.Api.Data;
using DentalRay.Api.Models;

namespace DentalRay.Api.Services
{
    public sealed class StudyFinancialAuditService
    {
        private readonly DentalRayDbContext _context;

        public StudyFinancialAuditService(DentalRayDbContext context)
        {
            _context = context;
        }

        // این متد فقط رکورد جدید اضافه می‌کند؛ هیچ مسیر Update یا Delete برای Audit وجود ندارد.
        // ذخیره نهایی همراه با تغییر مالی در یک تراکنش توسط Controller انجام می‌شود.
        public void Record(
            ClaimsPrincipal user,
            int studyID,
            string entityType,
            int? entityID,
            string operation,
            object? before,
            object? after,
            string? notes = null)
        {
            int? userID = int.TryParse(
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                out var parsedUserID)
                    ? parsedUserID
                    : null;

            string? beforeJson = before == null
                ? null
                : JsonSerializer.Serialize(before, before.GetType());

            string? afterJson = after == null
                ? null
                : JsonSerializer.Serialize(after, after.GetType());

            _context.StudyFinancialAuditLogs.Add(new StudyFinancialAuditLog
            {
                StudyID = studyID,
                EntityType = entityType,
                EntityID = entityID,
                Operation = operation,
                UserID = userID,
                UserName = user.Identity?.Name,
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                Notes = notes,
                CreatedDate = DateTime.Now
            });
        }
    }
}
