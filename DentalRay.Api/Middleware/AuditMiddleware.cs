using System.Security.Claims;
using DentalRay.Api.Services;

namespace DentalRay.Api.Middleware
{
    public sealed class AuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditMiddleware> _logger;

        public AuditMiddleware(
            RequestDelegate next,
            ILogger<AuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            AuditService auditService)
        {
            bool shouldAudit =
                context.Request.Path.StartsWithSegments("/api") &&
                !context.Request.Path.StartsWithSegments("/api/auth");

            Exception? caughtException = null;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                caughtException = ex;
                throw;
            }
            finally
            {
                if (shouldAudit &&
                    context.User.Identity?.IsAuthenticated == true)
                {
                    try
                    {
                        int? userID = null;

                        string? idText =
                            context.User.FindFirstValue(
                                ClaimTypes.NameIdentifier);

                        if (int.TryParse(
                            idText,
                            out int parsedID))
                        {
                            userID = parsedID;
                        }

                        int statusCode =
                            caughtException == null
                                ? context.Response.StatusCode
                                : StatusCodes.Status500InternalServerError;

                        await auditService.LogAsync(
                            action:
                                context.Request.Method +
                                " " +
                                context.Request.Path,
                            userID: userID,
                            userName:
                                context.User.Identity.Name ??
                                "Unknown",
                            isSuccess:
                                statusCode < 400,
                            details:
                                context.Request.QueryString.HasValue
                                    ? context.Request.QueryString.Value
                                    : null,
                            httpContext: context,
                            statusCode: statusCode,
                            httpMethod:
                                context.Request.Method,
                            path:
                                context.Request.Path);
                    }
                    catch (Exception auditException)
                    {
                        _logger.LogError(
                            auditException,
                            "DentalRay audit logging failed.");
                    }
                }
            }
        }
    }
}
