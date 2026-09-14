using DentalRay.Api.Data;
using DentalRay.Api.Models;

namespace DentalRay.Api.Services
{
    public sealed class AuditService
    {
        private readonly DentalRayDbContext _context;

        public AuditService(
            DentalRayDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            string action,
            int? userID = null,
            string? userName = null,
            bool isSuccess = true,
            string? details = null,
            HttpContext? httpContext = null,
            int? statusCode = null,
            string? httpMethod = null,
            string? path = null)
        {
            if (details?.Length > 2000)
            {
                details = details[..2000];
            }

            string? userAgent =
                httpContext?
                    .Request
                    .Headers
                    .UserAgent
                    .ToString();

            if (userAgent?.Length > 500)
            {
                userAgent = userAgent[..500];
            }

            _context.AuditLogs.Add(
                new AuditLog
                {
                    UserID = userID,
                    UserName = userName,
                    Action = action,
                    HttpMethod = httpMethod,
                    Path = path,
                    StatusCode = statusCode,
                    IsSuccess = isSuccess,
                    Details = details,
                    IpAddress =
                        httpContext?
                            .Connection
                            .RemoteIpAddress?
                            .ToString(),
                    UserAgent = userAgent,
                    CreatedDate = DateTime.Now
                });

            await _context.SaveChangesAsync();
        }
    }
}
