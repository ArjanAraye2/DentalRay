using DentalRay.Api.Data;
using DentalRay.Api.Models;
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

        public AuthController(DentalRayDbContext context, IPasswordHasher<User> passwordHasher, IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        public sealed class LoginRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            string userName = (request.UserName ?? string.Empty).Trim();
            if (userName.Length == 0 || string.IsNullOrEmpty(request.Password))
                return BadRequest(new { success = false, message = "UserName and Password are required." });

            // ------------------------------------------------------------
            // Local Super Admin
            // ------------------------------------------------------------
            // There is deliberately no tblUsers row for this account. Both the
            // username and password hash come from DentalRay.config.json on the
            // server. No Super Admin secret is committed to GitHub or SQL Server.
            if (string.Equals(userName, _configuration["SuperAdmin:UserName"], StringComparison.OrdinalIgnoreCase))
            {
                string? superHash = _configuration["SuperAdmin:PasswordHash"];
                if (!string.IsNullOrWhiteSpace(superHash))
                {
                    // PasswordHasher requires a User instance only as contextual
                    // input; this temporary object is never written to the database.
                    var superUser = new User { UserName = userName };
                    var result = _passwordHasher.VerifyHashedPassword(superUser, superHash, request.Password);
                    if (result != PasswordVerificationResult.Failed)
                    {
                        return Ok(new
                        {
                            success = true,
                            user = new
                            {
                                UserID = 0,
                                UserName = userName,
                                StaffID = 0,
                                FirstName = "مدیر",
                                LastName = "سیستم",
                                StaffType = 0,
                                IsSuperAdmin = true
                            }
                        });
                    }
                }

                return Unauthorized(new { success = false, message = "Invalid username or password." });
            }

            // ------------------------------------------------------------
            // Normal database User
            // ------------------------------------------------------------
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserName == userName);
            if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            var staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.StaffID == user.StaffID);
            if (staff == null)
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            var today = DateTime.Today;
            if (staff.StartDate.Date > today || (staff.EndDate.HasValue && staff.EndDate.Value.Date < today))
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            return Ok(new
            {
                success = true,
                user = new
                {
                    user.UserID,
                    user.UserName,
                    staff.StaffID,
                    staff.FirstName,
                    staff.LastName,
                    staff.StaffType,
                    IsSuperAdmin = false
                }
            });
        }
    }
}
