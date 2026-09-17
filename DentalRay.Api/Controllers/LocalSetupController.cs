using DentalRay.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace DentalRay.Api.Controllers
{
    // TEMPORARY setup endpoint.
    // This controller exists only to generate the first Super Admin password hash.
    // It accepts requests exclusively from the DentalRay server itself (localhost).
    // After the hash has been generated and saved in DentalRay.config.json, this
    // controller should be removed from the project.
    [ApiController]
    [Route("api/local-setup")]
    public class LocalSetupController : ControllerBase
    {
        private readonly IPasswordHasher<User> _passwordHasher;

        public LocalSetupController(IPasswordHasher<User> passwordHasher)
        {
            _passwordHasher = passwordHasher;
        }

        public sealed class HashPasswordRequest
        {
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("hash-password")]
        public IActionResult HashPassword(HashPasswordRequest request)
        {
            // Never expose this utility to another computer on the clinic LAN.
            IPAddress? remoteAddress = HttpContext.Connection.RemoteIpAddress;
            if (remoteAddress == null || !IPAddress.IsLoopback(remoteAddress))
                return NotFound();

            if (string.IsNullOrEmpty(request.Password))
                return BadRequest(new { success = false, message = "Password is required." });

            // This User object is temporary and is never stored in SQL Server.
            var temporaryUser = new User { UserName = "LocalSetup" };
            string hash = _passwordHasher.HashPassword(temporaryUser, request.Password);

            return Ok(new
            {
                success = true,
                passwordHash = hash
            });
        }
    }
}
