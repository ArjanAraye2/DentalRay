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

        public AuthController(DentalRayDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
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

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserName == userName);

            // Use the same generic response for unknown users and bad passwords so
            // the Login API does not disclose whether a username exists.
            if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            var staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.StaffID == user.StaffID);
            if (staff == null)
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            // A staff member whose employment has ended is not allowed to log in.
            var today = DateTime.Today;
            if (staff.StartDate.Date > today || (staff.EndDate.HasValue && staff.EndDate.Value.Date < today))
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            // This response intentionally contains no password/hash. Session/token
            // issuance will be the next authentication step.
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
                    staff.StaffType
                }
            });
        }
    }
}
