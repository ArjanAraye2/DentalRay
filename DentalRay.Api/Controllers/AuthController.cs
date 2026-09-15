using System.Security.Claims;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly AuditService _auditService;

        public AuthController(
            DentalRayDbContext context,
            PasswordService passwordService,
            AuditService auditService)
        {
            _context = context;
            _passwordService = passwordService;
            _auditService = auditService;
        }

        [AllowAnonymous]
        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            bool hasUsers =
                await _context.Users
                    .AsNoTracking()
                    .AnyAsync();

            if (!hasUsers)
            {
                return Ok(new
                {
                    authenticated = false,
                    needsBootstrap = true
                });
            }

            if (User.Identity?.IsAuthenticated != true)
            {
                return Ok(new
                {
                    authenticated = false,
                    needsBootstrap = false
                });
            }

            return Ok(new
            {
                authenticated = true,
                needsBootstrap = false,
                user = CurrentUserResponse()
            });
        }

        [AllowAnonymous]
        [HttpPost("bootstrap")]
        public async Task<IActionResult> Bootstrap(
            BootstrapAdminRequest request)
        {
            if (await _context.Users.AnyAsync())
            {
                return Conflict(new
                {
                    message =
                        "مدیر اولیه قبلاً ایجاد شده است."
                });
            }

            string? error =
                ValidateUserInput(
                    request.UserName,
                    request.DisplayName,
                    request.Password);

            if (error != null)
            {
                return BadRequest(new
                {
                    message = error
                });
            }

            var user =
                new AppUser
                {
                    UserName =
                        request.UserName.Trim(),
                    DisplayName =
                        request.DisplayName.Trim(),
                    PasswordHash =
                        _passwordService.HashPassword(
                            request.Password),
                    Role = "Admin",
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // If a database was upgraded before its first administrator was
            // created, claim legacy records that could not be assigned by the
            // schema migration. New resources always receive an owner at creation.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.tblPatients SET OwnerUserID = {user.UserID} WHERE OwnerUserID IS NULL;");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.tblRadiologyStudies SET OwnerUserID = {user.UserID} WHERE OwnerUserID IS NULL;");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE image SET OwnerUserID = COALESCE(study.OwnerUserID, {user.UserID}) FROM dbo.tblRadiologyImages AS image LEFT JOIN dbo.tblRadiologyStudies AS study ON study.StudyID = image.StudyID WHERE image.OwnerUserID IS NULL;");

            await _auditService.LogAsync(
                action: "BootstrapAdmin",
                userID: user.UserID,
                userName: user.UserName,
                details:
                    "Initial administrator created.",
                httpContext: HttpContext);

            await SignInAsync(user);

            return Ok(new
            {
                success = true,
                user = ToUserResponse(user)
            });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginRequest request)
        {
            string userName =
                request.UserName.Trim();

            var user =
                await _context.Users
                    .FirstOrDefaultAsync(
                        item =>
                            item.UserName == userName);

            bool valid =
                user != null &&
                user.IsActive &&
                _passwordService.VerifyPassword(
                    request.Password,
                    user.PasswordHash);

            if (!valid)
            {
                await _auditService.LogAsync(
                    action: "LoginFailed",
                    userID: user?.UserID,
                    userName: userName,
                    isSuccess: false,
                    details:
                        user != null &&
                        !user.IsActive
                            ? "Inactive user."
                            : "Invalid credentials.",
                    httpContext: HttpContext,
                    statusCode:
                        StatusCodes.Status401Unauthorized,
                    httpMethod: Request.Method,
                    path: Request.Path);

                return Unauthorized(new
                {
                    message =
                        "نام کاربری یا رمز عبور صحیح نیست."
                });
            }

            user!.LastLoginDate = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "LoginSuccess",
                userID: user.UserID,
                userName: user.UserName,
                httpContext: HttpContext,
                statusCode:
                    StatusCodes.Status200OK,
                httpMethod: Request.Method,
                path: Request.Path);

            await SignInAsync(user);

            return Ok(new
            {
                success = true,
                user = ToUserResponse(user)
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _auditService.LogAsync(
                action: "Logout",
                userID: GetCurrentUserID(),
                userName:
                    User.Identity?.Name ??
                    string.Empty,
                httpContext: HttpContext,
                statusCode:
                    StatusCodes.Status200OK,
                httpMethod: Request.Method,
                path: Request.Path);

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok(new
            {
                success = true
            });
        }

        private async Task SignInAsync(
            AppUser user)
        {
            var claims =
                new List<Claim>
                {
                    new(
                        ClaimTypes.NameIdentifier,
                        user.UserID.ToString()),
                    new(
                        ClaimTypes.Name,
                        user.UserName),
                    new(
                        ClaimTypes.Role,
                        user.Role),
                    new(
                        "display_name",
                        user.DisplayName)
                };

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true
                });
        }

        private object CurrentUserResponse()
        {
            return new
            {
                userID = GetCurrentUserID(),
                userName = User.Identity?.Name,
                displayName =
                    User.FindFirstValue("display_name"),
                role =
                    User.FindFirstValue(ClaimTypes.Role)
            };
        }

        private int? GetCurrentUserID()
        {
            string? text =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                text,
                out int id)
                ? id
                : null;
        }

        private static object ToUserResponse(
            AppUser user)
        {
            return new
            {
                user.UserID,
                user.UserName,
                user.DisplayName,
                user.Role,
                user.IsActive,
                user.LastLoginDate
            };
        }

        private static string? ValidateUserInput(
            string userName,
            string displayName,
            string password)
        {
            if (string.IsNullOrWhiteSpace(userName) ||
                userName.Trim().Length < 3 ||
                userName.Trim().Length > 50)
            {
                return
                    "نام کاربری باید بین ۳ تا ۵۰ کاراکتر باشد.";
            }

            if (string.IsNullOrWhiteSpace(displayName) ||
                displayName.Trim().Length > 100)
            {
                return
                    "نام نمایشی کاربر را وارد کنید.";
            }

            if (string.IsNullOrWhiteSpace(password) ||
                password.Length < 8)
            {
                return
                    "رمز عبور باید حداقل ۸ کاراکتر باشد.";
            }

            return null;
        }
    }
}
