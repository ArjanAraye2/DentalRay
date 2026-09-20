using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Security;
using DentalRay.Api.Services.Pos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    // POS (card reader) configuration. Restricted to SuperAdmin: these settings
    // decide where money is sent, so ordinary accounts must not change them.
    [ApiController]
    [Route("api/pos")]
    [SuperAdminOnly]
    public class PosSettingsController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        private readonly PosProtocolRegistry _protocols;
        private readonly ILogger<PosSettingsController> _logger;

        public PosSettingsController(DentalRayDbContext db, PosProtocolRegistry protocols, ILogger<PosSettingsController> logger)
        {
            _db = db;
            _protocols = protocols;
            _logger = logger;
        }

        public sealed class PosSettingRequest
        {
            public string Name { get; set; } = string.Empty;
            public byte ConnectionType { get; set; } = 1;
            public string? Host { get; set; }
            public int? Port { get; set; }
            public string? ComPort { get; set; }
            public int? BaudRate { get; set; }
            public string Protocol { get; set; } = "Generic";
            public string? RequestPattern { get; set; }
            public string? SuccessPattern { get; set; }
            public string Encoding { get; set; } = "UTF8";
            public int TimeoutSeconds { get; set; } = 5;
            public bool IsActive { get; set; } = true;
            public bool IsDefault { get; set; }
        }

        [HttpGet("protocols")]
        public IActionResult Protocols() =>
            Ok(new { success = true, protocols = _protocols.Available.Select(p => new { name = p.Name, description = p.Description }) });

        [HttpGet("settings")]
        public async Task<IActionResult> List()
        {
            var rows = await _db.PosSettings.AsNoTracking()
                .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync();
            return Ok(new { success = true, settings = rows });
        }

        [HttpPost("settings")]
        public async Task<IActionResult> Create(PosSettingRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(new { success = false, message = error });

            if (request.IsDefault)
                await ClearDefaultAsync();

            var row = new PosSetting
            {
                Name = request.Name.Trim(),
                ConnectionType = request.ConnectionType,
                Host = Clean(request.Host),
                Port = request.Port,
                ComPort = Clean(request.ComPort),
                BaudRate = request.BaudRate,
                Protocol = string.IsNullOrWhiteSpace(request.Protocol) ? "Generic" : request.Protocol.Trim(),
                RequestPattern = Clean(request.RequestPattern),
                SuccessPattern = Clean(request.SuccessPattern),
                Encoding = string.IsNullOrWhiteSpace(request.Encoding) ? "UTF8" : request.Encoding.Trim(),
                TimeoutSeconds = request.TimeoutSeconds <= 0 ? 5 : request.TimeoutSeconds,
                IsActive = request.IsActive,
                IsDefault = request.IsDefault,
                CreatedDate = DateTime.Now
            };
            _db.PosSettings.Add(row);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, setting = row });
        }

        [HttpPut("settings/{id:int}")]
        public async Task<IActionResult> Update(int id, PosSettingRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(new { success = false, message = error });

            var row = await _db.PosSettings.FirstOrDefaultAsync(x => x.PosSettingID == id);
            if (row == null) return NotFound(new { success = false, message = "تنظیمات پوز پیدا نشد." });

            if (request.IsDefault && !row.IsDefault)
                await ClearDefaultAsync();

            row.Name = request.Name.Trim();
            row.ConnectionType = request.ConnectionType;
            row.Host = Clean(request.Host);
            row.Port = request.Port;
            row.ComPort = Clean(request.ComPort);
            row.BaudRate = request.BaudRate;
            row.Protocol = string.IsNullOrWhiteSpace(request.Protocol) ? "Generic" : request.Protocol.Trim();
            row.RequestPattern = Clean(request.RequestPattern);
            row.SuccessPattern = Clean(request.SuccessPattern);
            row.Encoding = string.IsNullOrWhiteSpace(request.Encoding) ? "UTF8" : request.Encoding.Trim();
            row.TimeoutSeconds = request.TimeoutSeconds <= 0 ? 5 : request.TimeoutSeconds;
            row.IsActive = request.IsActive;
            row.IsDefault = request.IsDefault;
            row.ModifiedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, setting = row });
        }

        [HttpDelete("settings/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _db.PosSettings.FirstOrDefaultAsync(x => x.PosSettingID == id);
            if (row == null) return NotFound(new { success = false, message = "تنظیمات پوز پیدا نشد." });
            _db.PosSettings.Remove(row);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Tests connectivity. A wrong IP is the most common problem, so the test
        // reports the actual reason instead of just failing.
        [HttpPost("settings/{id:int}/test")]
        public async Task<IActionResult> Test(int id, CancellationToken cancellationToken)
        {
            var row = await _db.PosSettings.FirstOrDefaultAsync(x => x.PosSettingID == id);
            if (row == null) return NotFound(new { success = false, message = "تنظیمات پوز پیدا نشد." });

            var result = await SendAsync(row, 1000m, "TEST", cancellationToken);

            row.LastTestAt = DateTime.Now;
            row.LastTestMessage = result.Success
                ? result.Message
                : $"{result.Message}{(string.IsNullOrWhiteSpace(result.RawResponse) ? "" : " | " + result.RawResponse)}";
            await _db.SaveChangesAsync();

            return Ok(new { success = true, ok = result.Success, message = result.Message, raw = result.RawResponse });
        }

        private async Task<PosResult> SendAsync(PosSetting row, decimal amount, string invoice, CancellationToken cancellationToken)
        {
            if (row.ConnectionType != 1)
                return PosResult.Fail("این نوع اتصال هنوز پشتیبانی نمی‌شود. فعلاً اتصال شبکه‌ای (TCP/IP) فعال است.");

            var protocol = _protocols.Resolve(row.Protocol);
            return await protocol.SendAmountAsync(new PosRequest
            {
                Host = row.Host ?? string.Empty,
                Port = row.Port ?? 0,
                Amount = amount,
                Invoice = invoice,
                RequestPattern = row.RequestPattern,
                SuccessPattern = row.SuccessPattern,
                Encoding = row.Encoding,
                TimeoutSeconds = row.TimeoutSeconds
            }, cancellationToken);
        }

        private async Task ClearDefaultAsync()
        {
            foreach (var other in await _db.PosSettings.Where(x => x.IsDefault).ToListAsync())
                other.IsDefault = false;
        }

        private static string? Validate(PosSettingRequest x)
        {
            if (string.IsNullOrWhiteSpace(x.Name)) return "نام دستگاه را وارد کنید.";
            if (x.Name.Trim().Length > 100) return "نام دستگاه نمی‌تواند بیشتر از ۱۰۰ نویسه باشد.";
            if (x.ConnectionType is < 1 or > 4) return "نوع اتصال معتبر نیست.";
            if (x.ConnectionType == 1)
            {
                if (string.IsNullOrWhiteSpace(x.Host)) return "آدرس IP پوز را وارد کنید.";
                if (x.Host.Trim().Length > 100) return "آدرس IP معتبر نیست.";
                if (x.Port is null or < 1 or > 65535) return "پورت پوز باید بین ۱ تا ۶۵۵۳۵ باشد.";
            }
            if (x.TimeoutSeconds is < 0 or > 60) return "مهلت انتظار باید بین ۱ تا ۶۰ ثانیه باشد.";
            if (x.RequestPattern is { Length: > 500 }) return "الگوی درخواست نمی‌تواند بیشتر از ۵۰۰ نویسه باشد.";
            if (x.SuccessPattern is { Length: > 200 }) return "الگوی موفقیت نمی‌تواند بیشتر از ۲۰۰ نویسه باشد.";
            return null;
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
