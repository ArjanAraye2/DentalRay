using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/communications")]
    [Authorize]
    public sealed class CommunicationController : ControllerBase
    {
        private readonly ICommunicationService _communication;
        private readonly DentalRayDbContext _context;

        public CommunicationController(ICommunicationService communication, DentalRayDbContext context)
        {
            _communication = communication;
            _context = context;
        }

        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            if (!IsSuperAdmin()) return Forbid();
            var settings = _communication.GetSettings();
            settings.SmsApiKey = string.IsNullOrEmpty(settings.SmsApiKey) ? string.Empty : "••••••••";
            return Ok(new { success = true, settings });
        }

        [HttpPut("settings")]
        public async Task<IActionResult> SaveSettings(CommunicationChannelSettings settings)
        {
            if (!IsSuperAdmin()) return Forbid();
            var current = _communication.GetSettings();
            if (settings.SmsApiKey == "••••••••") settings.SmsApiKey = current.SmsApiKey;
            await _communication.SaveSettingsAsync(settings);
            return Ok(new { success = true });
        }

        [HttpPost("sms/test")]
        public async Task<IActionResult> TestSms([FromBody] SendSmsRequest request)
        {
            if (!IsSuperAdmin()) return Forbid();
            var result = await _communication.SendSmsAsync(request.Mobile, string.IsNullOrWhiteSpace(request.Message) ? "تست اتصال DentalRay" : request.Message);
            return result.Success ? Ok(new { success = true, message = result.Message }) : BadRequest(new { success = false, message = result.Message });
        }

        [HttpPost("sms/send")]
        public async Task<IActionResult> SendSms([FromBody] SendSmsRequest request)
        {
            if (request.PatientID.HasValue)
            {
                var patient = await _context.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.PatientID == request.PatientID.Value);
                if (patient == null) return NotFound(new { success = false, message = "بیمار پیدا نشد." });
                if (string.IsNullOrWhiteSpace(request.Mobile)) request.Mobile = patient.Mobile ?? string.Empty;
            }
            var result = await _communication.SendSmsAsync(request.Mobile, request.Message);
            return result.Success ? Ok(new { success = true, message = result.Message }) : BadRequest(new { success = false, message = result.Message });
        }

        private bool IsSuperAdmin() => string.Equals(User.FindFirst("IsSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
