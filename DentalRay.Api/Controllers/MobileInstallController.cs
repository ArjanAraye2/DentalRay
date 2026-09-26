using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalRay.Api.Controllers
{
    // ============================================================
    // Installing Dentix on the clinic phone from Dentix itself.
    //
    // Android never lets an app install another one silently, so the only
    // honest way to skip taps is adb: the phone is attached once (USB, or once
    // over Wi-Fi after `adb tcpip`), and from then on the clinic presses one
    // button here and the package lands on the phone.
    //
    // Everything runs on the machine that hosts Dentix, so adb must exist on it;
    // the answer is reported honestly instead of pretending it worked.
    // ============================================================
    [ApiController]
    [Route("api/install")]
    public class MobileInstallController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public MobileInstallController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public sealed record DeviceRequest(string? Serial);
        public sealed record ConnectRequest(string? Ip);

        public sealed record AdbDevice(string Serial, string State, string Model);

        /// <summary>What is attached right now, and whether this server can install.</summary>
        [HttpGet("status")]
        public IActionResult Status()
        {
            string? adb = FindAdb();
            string? apk = FindApk();
            var devices = adb == null ? new List<AdbDevice>() : ParseDevices(RunAdb(adb, "devices -l", 6000).Output);

            return Ok(new
            {
                success = true,
                adbFound = adb != null,
                adbPath = adb,
                apkFound = apk != null,
                apkName = apk == null ? null : Path.GetFileName(apk),
                apkSize = apk == null ? 0 : new FileInfo(apk).Length,
                devices
            });
        }

        /// <summary>Install the newest build on one attached device.</summary>
        [HttpPost("run")]
        public IActionResult Install(DeviceRequest request)
        {
            string? adb = FindAdb();
            string? apk = FindApk();
            if (adb == null) return BadRequest(new { success = false, message = "برنامهٔ adb روی این سرور پیدا نشد (MobileApp:AdbPath را تنظیم کنید)." });
            if (apk == null) return BadRequest(new { success = false, message = "فایل نصب پیدا نشد؛ ابتدا برنامهٔ اندروید را بسازید." });

            var devices = ParseDevices(RunAdb(adb, "devices -l", 6000).Output);
            if (devices.Count == 0)
                return BadRequest(new { success = false, message = "گوشی‌ای متصل نیست. کابل را وصل و اشکال‌زدایی USB را روشن کنید." });

            string serial = request.Serial?.Trim() ?? devices[0].Serial;
            if (string.IsNullOrWhiteSpace(serial))
                return BadRequest(new { success = false, message = "گوشی‌ای برای نصب انتخاب نشده." });

            var result = RunAdb(adb, $"-s {serial} install -r \"{apk}\"", 180_000);
            bool ok = result.ExitCode == 0 && result.Output.Contains("Success", StringComparison.OrdinalIgnoreCase);

            return Ok(new
            {
                success = ok,
                serial,
                output = result.Output.Trim(),
                message = ok
                    ? $"برنامه با موفقیت روی {serial} نصب شد."
                    : $"نصب انجام نشد: {result.Output.Trim()}"
            });
        }

        /// <summary>
        /// One-time switch to Wi-Fi: while the phone is on the cable, it starts
        /// listening on port 5555 and reports its address. From then on the
        /// clinic can install without any cable.
        /// </summary>
        [HttpPost("wireless-enable")]
        public IActionResult WirelessEnable(DeviceRequest request)
        {
            string? adb = FindAdb();
            if (adb == null) return BadRequest(new { success = false, message = "برنامهٔ adb پیدا نشد." });
            if (string.IsNullOrWhiteSpace(request.Serial))
                return BadRequest(new { success = false, message = "ابتدا گوشی را با کابل وصل کنید." });

            string serial = request.Serial.Trim();
            var port = RunAdb(adb, $"-s {serial} tcpip 5555", 20_000);
            var addr = RunAdb(adb, $"-s {serial} shell ip -f inet addr show wlan0", 15_000);
            string? ip = ParseWlanAddress(addr.Output);

            if (string.IsNullOrEmpty(ip))
                return Ok(new
                {
                    success = false,
                    message = "اتصال بی‌سیم فعال شد ولی آدرس وای‌فای گوشی خوانده نشد؛ آدرس IP گوشی را دستی وارد کنید.",
                    port = 5555
                });

            return Ok(new { success = true, ip, port = 5555, message = $"آماده است: {ip}:5555" });
        }

        /// <summary>Attach a phone that already listens on the LAN.</summary>
        [HttpPost("connect")]
        public IActionResult Connect(ConnectRequest request)
        {
            string? adb = FindAdb();
            if (adb == null) return BadRequest(new { success = false, message = "برنامهٔ adb پیدا نشد." });

            string ip = (request.Ip ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ip)) return BadRequest(new { success = false, message = "آدرس IP گوشی را وارد کنید." });
            string target = ip.Contains(':') ? ip : $"{ip}:5555";

            var connected = RunAdb(adb, $"connect {target}", 15_000);
            var devices = ParseDevices(RunAdb(adb, "devices -l", 6000).Output);
            bool attached = devices.Any(d => d.Serial.StartsWith(ip, StringComparison.Ordinal) || d.Serial == target);

            return Ok(new
            {
                success = attached,
                output = connected.Output.Trim(),
                devices,
                message = attached ? $"به {target} وصل شد." : "اتصال برقرار نشد؛ آدرس و اتصال گوشی را بررسی کنید."
            });
        }

        // ------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------
        private string? FindAdb()
        {
            string? configured = _configuration["MobileApp:AdbPath"]?.Trim();
            if (!string.IsNullOrWhiteSpace(configured) && System.IO.File.Exists(configured)) return configured;

            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Android", "Sdk", "platform-tools", "adb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Android", "platform-tools", "adb.exe"),
                @"C:\Android\platform-tools\adb.exe"
            };
            foreach (string candidate in candidates)
                if (System.IO.File.Exists(candidate)) return candidate;

            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                string path = Path.Combine(dir.Trim(), "adb.exe");
                if (System.IO.File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>Newest build, so a rebuilt app is the one that gets installed.</summary>
        private string? FindApk()
        {
            string? configured = _configuration["MobileApp:ApkPath"]?.Trim();
            if (!string.IsNullOrWhiteSpace(configured))
            {
                string full = Path.IsPathRooted(configured) ? configured : Path.Combine(_environment.ContentRootPath, configured);
                if (System.IO.File.Exists(full)) return full;
            }

            string root = _environment.ContentRootPath;
            foreach (string dir in new[]
                     {
                         Path.Combine(root, "DentalRay.Mobile", "dist"),
                         Path.Combine(root, "..", "DentalRay.Mobile", "app", "build", "outputs", "apk", "debug"),
                         Path.Combine(root, "wwwroot", "install")
                     })
            {
                if (!Directory.Exists(dir)) continue;
                string? newest = Directory.GetFiles(dir, "*.apk")
                    .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                    .FirstOrDefault();
                if (newest != null) return newest;
            }
            return null;
        }

        private sealed record AdbResult(int ExitCode, string Output);

        private static AdbResult RunAdb(string adb, string arguments, int timeoutMs)
        {
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = adb,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using var process = Process.Start(start);
                if (process == null) return new AdbResult(-1, "adb اجرا نشد.");

                var output = new StringBuilder();
                process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(true); } catch { /* already gone */ }
                    return new AdbResult(-1, "زمان اجرا تمام شد.");
                }
                process.WaitForExit();
                return new AdbResult(process.ExitCode, output.ToString());
            }
            catch (Exception ex)
            {
                return new AdbResult(-1, ex.Message);
            }
        }

        private static List<AdbDevice> ParseDevices(string output)
        {
            var devices = new List<AdbDevice>();
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("*", StringComparison.OrdinalIgnoreCase)) continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (parts[1] != "device" && parts[1] != "offline" && parts[1] != "unauthorized") continue;

                string model = "";
                int modelIndex = Array.IndexOf(parts, "model:");
                if (modelIndex >= 0 && modelIndex + 1 < parts.Length) model = parts[modelIndex + 1];

                devices.Add(new AdbDevice(parts[0], parts[1], model));
            }
            return devices;
        }

        /// <summary>10.x.x.x or 192.168.x.x from `ip addr show wlan0`.</summary>
        private static string? ParseWlanAddress(string output)
        {
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                int at = line.IndexOf("inet ", StringComparison.Ordinal);
                if (at < 0) continue;
                string rest = line[(at + 5)..];
                string address = rest.Split('/')[0].Trim();
                if (System.Net.IPAddress.TryParse(address, out var parsed) &&
                    parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !System.Net.IPAddress.IsLoopback(parsed))
                    return address;
            }
            return null;
        }
    }
}
