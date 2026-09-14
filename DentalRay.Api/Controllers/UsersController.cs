using System.Security.Claims;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "AdminOnly")]
    public class UsersController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly PasswordService _passwordService;

        public UsersController(
            DentalRayDbContext context,
            PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users =
                await _context.Users
                    .AsNoTracking()
                    .OrderBy(item => item.UserName)
                    .Select(item => new
                    {
                        item.UserID,
                        item.UserName,
                        item.DisplayName,
                        item.Role,
                        item.IsActive,
                        item.CreatedDate,
                        item.ModifiedDate,
                        item.LastLoginDate
                    })
                    .ToListAsync();

            return Ok(new { users });
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(
            CreateUserRequest request)
        {
            string userName =
                request.UserName.Trim();

            string displayName =
                request.DisplayName.Trim();

            string role =
                NormalizeRole(
                    request.Role);

            string? error =
                ValidateUser(
                    userName,
                    displayName,
                    request.Password,
                    role);

            if (error != null)
            {
                return BadRequest(new
                {
                    message = error
                });
            }

            if (await _context.Users.AnyAsync(
                item =>
                    item.UserName == userName))
            {
                return Conflict(new
                {
                    message =
                        "این نام کاربری قبلاً ثبت شده است."
                });
            }

            var user =
                new AppUser
                {
                    UserName = userName,
                    DisplayName = displayName,
                    PasswordHash =
                        _passwordService.HashPassword(
                            request.Password),
                    Role = role,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                userID = user.UserID
            });
        }

        [HttpPut("{userID:int}")]
        public async Task<IActionResult> UpdateUser(
            int userID,
            UpdateUserRequest request)
        {
            var user =
                await _context.Users
                    .FirstOrDefaultAsync(
                        item =>
                            item.UserID == userID);

            if (user == null)
            {
                return NotFound(new
                {
                    message =
                        "کاربر پیدا نشد."
                });
            }

            string userName =
                request.UserName.Trim();

            string displayName =
                request.DisplayName.Trim();

            string role =
                NormalizeRole(
                    request.Role);

            if (string.IsNullOrWhiteSpace(userName) ||
                userName.Length < 3 ||
                userName.Length > 50)
            {
                return BadRequest(new
                {
                    message =
                        "نام کاربری باید بین ۳ تا ۵۰ کاراکتر باشد."
                });
            }

            if (string.IsNullOrWhiteSpace(displayName) ||
                displayName.Length > 100)
            {
                return BadRequest(new
                {
                    message =
                        "نام نمایشی کاربر را وارد کنید."
                });
            }

            int currentUserID =
                GetCurrentUserID();

            if (userID == currentUserID &&
                (!request.IsActive ||
                 role != "Admin"))
            {
                return BadRequest(new
                {
                    message =
                        "مدیر واردشده نمی‌تواند حساب خودش را غیرفعال یا نقش خودش را از Admin خارج کند."
                });
            }

            if (user.Role == "Admin" &&
                user.IsActive &&
                (!request.IsActive ||
                 role != "Admin"))
            {
                int adminCount =
                    await _context.Users.CountAsync(
                        item =>
                            item.IsActive &&
                            item.Role == "Admin");

                if (adminCount <= 1)
                {
                    return BadRequest(new
                    {
                        message =
                            "حداقل یک مدیر فعال باید در سیستم باقی بماند."
                    });
                }
            }

            if (await _context.Users.AnyAsync(
                item =>
                    item.UserID != userID &&
                    item.UserName == userName))
            {
                return Conflict(new
                {
                    message =
                        "این نام کاربری قبلاً ثبت شده است."
                });
            }

            user.UserName = userName;
            user.DisplayName = displayName;
            user.Role = role;
            user.IsActive = request.IsActive;
            user.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true
            });
        }

        [HttpPost("{userID:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(
            int userID,
            ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 8)
            {
                return BadRequest(new
                {
                    message =
                        "رمز عبور باید حداقل ۸ کاراکتر باشد."
                });
            }

            var user =
                await _context.Users
                    .FirstOrDefaultAsync(
                        item =>
                            item.UserID == userID);

            if (user == null)
            {
                return NotFound(new
                {
                    message =
                        "کاربر پیدا نشد."
                });
            }

            user.PasswordHash =
                _passwordService.HashPassword(
                    request.Password);

            user.ModifiedDate =
                DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true
            });
        }

        private int GetCurrentUserID()
        {
            string? text =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                text,
                out int id)
                ? id
                : 0;
        }

        private static string NormalizeRole(
            string? role)
        {
            return string.Equals(
                role,
                "Admin",
                StringComparison.OrdinalIgnoreCase)
                ? "Admin"
                : "User";
        }

        private static string? ValidateUser(
            string userName,
            string displayName,
            string password,
            string role)
        {
            if (string.IsNullOrWhiteSpace(userName) ||
                userName.Length < 3 ||
                userName.Length > 50)
            {
                return
                    "نام کاربری باید بین ۳ تا ۵۰ کاراکتر باشد.";
            }

            if (string.IsNullOrWhiteSpace(displayName) ||
                displayName.Length > 100)
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

            if (role != "Admin" &&
                role != "User")
            {
                return
                    "نقش کاربر معتبر نیست.";
            }

            return null;
        }
    }
}
