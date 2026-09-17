using System.Security.Claims;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        public AuthController(DentalRayDbContext context, IPasswordHasher<User> passwordHasher, IConfiguration configuration) { _context = context; _passwordHasher = passwordHasher; _configuration = configuration; }

        public sealed class LoginRequest { public string UserName { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            string userName = (request.UserName ?? string.Empty).Trim();
            if (userName.Length == 0 || string.IsNullOrEmpty(request.Password)) return BadRequest(new { success = false, message = "UserName and Password are required." });

            if (string.Equals(userName, _configuration["SuperAdmin:UserName"], StringComparison.OrdinalIgnoreCase))
            {
                string? superHash = _configuration["SuperAdmin:PasswordHash"];
                if (!string.IsNullOrWhiteSpace(superHash))
                {
                    var superUser = new User { UserName = userName };
                    var result = _passwordHasher.VerifyHashedPassword(superUser, superHash, request.Password);
                    if (result != PasswordVerificationResult.Failed)
                    {
                        var identity = new LoginIdentity(0, userName, 0, "مدیر", "سیستم", 0, true);
                        await SignInAsync(identity);
                        return Ok(new { success = true, user = identity });
                    }
                }
                return Unauthorized(new { success = false, message = "Invalid username or password." });
            }

            // For every normal account, the login name is the staff member's Iranian National Code.
            // The configured SuperAdmin account above is the only exception.
            var user = await _context.Users.AsNoTracking()
                .Join(_context.Staff.AsNoTracking(),
                    account => account.StaffID,
                    staffMember => staffMember.StaffID,
                    (account, staffMember) => new { Account = account, Staff = staffMember })
                .Where(x => x.Staff.NationalCode == userName)
                .Select(x => x.Account)
                .FirstOrDefaultAsync();
            if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash)) return Unauthorized(new { success = false, message = "Invalid username or password." });
            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed) return Unauthorized(new { success = false, message = "Invalid username or password." });
            var staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.StaffID == user.StaffID);
            if (staff == null) return Unauthorized(new { success = false, message = "Invalid username or password." });
            var today = DateTime.Today;
            if (staff.StartDate.Date > today || (staff.EndDate.HasValue && staff.EndDate.Value.Date < today)) return Unauthorized(new { success = false, message = "Invalid username or password." });

            var normalIdentity = new LoginIdentity(user.UserID, staff.NationalCode, staff.StaffID, staff.FirstName, staff.LastName, staff.StaffType, false);
            await SignInAsync(normalIdentity);
            return Ok(new { success = true, user = normalIdentity });
        }

        // Used by the browser on page reload. The password is never needed again;
        // the server validates the protected HttpOnly cookie.
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new { success = true, user = IdentityFromClaims() });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { success = true });
        }

        private async Task SignInAsync(LoginIdentity identity)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, identity.UserName),
                new("UserID", identity.UserID.ToString()),
                new("StaffID", identity.StaffID.ToString()),
                new("StaffType", identity.StaffType.ToString()),
                new("FirstName", identity.FirstName),
                new("LastName", identity.LastName),
                new("IsSuperAdmin", identity.IsSuperAdmin ? "true" : "false")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
        }

        private LoginIdentity IdentityFromClaims()
        {
            int.TryParse(User.FindFirstValue("UserID"), out int userID);
            int.TryParse(User.FindFirstValue("StaffID"), out int staffID);
            byte.TryParse(User.FindFirstValue("StaffType"), out byte staffType);
            return new LoginIdentity(userID, User.Identity?.Name ?? string.Empty, staffID, User.FindFirstValue("FirstName") ?? string.Empty, User.FindFirstValue("LastName") ?? string.Empty, staffType, string.Equals(User.FindFirstValue("IsSuperAdmin"), "true", StringComparison.OrdinalIgnoreCase));
        }

        public sealed record LoginIdentity(int UserID, string UserName, int StaffID, string FirstName, string LastName, byte StaffType, bool IsSuperAdmin);
    }
}
