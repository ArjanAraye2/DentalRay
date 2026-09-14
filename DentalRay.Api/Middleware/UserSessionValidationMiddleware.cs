using System.Security.Claims;
using DentalRay.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Middleware
{
    public sealed class UserSessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public UserSessionValidationMiddleware(
            RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            DentalRayDbContext dbContext)
        {
            if (context.Request.Path.StartsWithSegments("/api") &&
                context.User.Identity?.IsAuthenticated == true)
            {
                string? idText =
                    context.User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                if (!int.TryParse(
                    idText,
                    out int userID))
                {
                    await RejectAsync(context);
                    return;
                }

                var user =
                    await dbContext.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            item =>
                                item.UserID == userID);

                string? role =
                    context.User.FindFirstValue(
                        ClaimTypes.Role);

                if (user == null ||
                    !user.IsActive ||
                    !string.Equals(
                        user.Role,
                        role,
                        StringComparison.Ordinal))
                {
                    await RejectAsync(context);
                    return;
                }
            }

            await _next(context);
        }

        private static async Task RejectAsync(
            HttpContext context)
        {
            await context.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;
        }
    }
}
