using System.Diagnostics;
using System.Text.Json;
using DentalRay.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Services;

/// <summary>
/// پشتیبان‌گیریِ خودکارِ دیتابیس و تصاویر.
///
/// دو قاعدهٔ ساده که برای مطبِ محلی کافی است:
///  - دیتابیس: فایلِ BAK با فشرده‌سازی، و فقط N نسخهٔ آخر نگه داشته می‌شود.
///  - تصاویر: کپیِ تکمیلی (فقط اضافه‌ها/تغییرها، بدونِ حذف) ⇒ اگر فایلی در مطب
///    اشتباهی پاک شد، در پشتیبان می‌ماند.
///
/// مسیر در DentalRay.config.json و بخش Backup قابلِ تغییر است؛ اجرای خودکار
/// هر ساعت بررسی می‌شود و اگر آخرین پشتیبان بیش از ۲۳ ساعت پیش بوده اجرا می‌شود —
/// یعنی اگر کامپیوتر مطب شب‌ها خاموش باشد، هنگامِ روشن شدن جبران می‌شود.
/// </summary>
public sealed class BackupService
{
    private readonly IConfiguration _configuration;
    private readonly DentalRayDbContext _db;
    private readonly RadiologyStorageService _storage;
    private readonly ILogger<BackupService> _logger;

    private static readonly object Gate = new();
    private static bool _running;

    public BackupService(IConfiguration configuration, DentalRayDbContext db,
        RadiologyStorageService storage, ILogger<BackupService> logger)
    {
        _configuration = configuration;
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public string RootPath
    {
        get
        {
            var configured = _configuration["Backup:RootPath"];
            return string.IsNullOrWhiteSpace(configured) ? @"D:\DentalRayBackup" : configured.Trim();
        }
    }

    public int KeepBackups
    {
        get
        {
            int n = int.TryParse(_configuration["Backup:KeepBackups"], out var v) ? v : 0;
            return n > 0 ? n : 7;
        }
    }

    private string DbFolder => Path.Combine(RootPath, "db");
    private string ImagesFolder => Path.Combine(RootPath, "images");
    private string StateFile => Path.Combine(RootPath, "backup-state.json");

    private sealed class BackupState
    {
        public DateTime? LastRun { get; set; }
        public bool Ok { get; set; }
        public string? Error { get; set; }
    }

    private BackupState ReadState()
    {
        try
        {
            if (File.Exists(StateFile))
                return JsonSerializer.Deserialize<BackupState>(File.ReadAllText(StateFile)) ?? new BackupState();
        }
        catch { /* وضعیتِ خراب ⇒ مثلِ اینکه پشتیبان نداشته‌ایم */ }
        return new BackupState();
    }

    private void WriteState(bool ok, string? error)
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(StateFile, JsonSerializer.Serialize(new BackupState
            {
                LastRun = DateTime.Now,
                Ok = ok,
                Error = error
            }));
        }
        catch (Exception e) { _logger.LogWarning("[DentalRay پشتیبان] نوشتنِ وضعیت نشد: {E}", e.Message); }
    }

    /// <summary>آیا نوبتِ پشتیبان رسیده؟ (بیش از ۲۳ ساعت از آخرین)</summary>
    public bool IsDue()
    {
        var state = ReadState();
        if (state.LastRun is null) return true;
        return DateTime.Now - state.LastRun.Value > TimeSpan.FromHours(23);
    }

    public bool IsRunning
    {
        get { lock (Gate) return _running; }
    }

    public async Task<(bool ok, string? error)> RunAsync(CancellationToken cancellationToken = default)
    {
        lock (Gate)
        {
            if (_running) return (false, "پشتیبان قبلاً در حال اجراست.");
            _running = true;
        }
        try
        {
            string? error = null;
            try
            {
                await RunCoreAsync(cancellationToken);
            }
            catch (Exception e)
            {
                error = e.Message;
                _logger.LogError(e, "[DentalRay پشتیبان] ناموفق");
            }
            WriteState(error is null, error);
            if (error is null) _logger.LogInformation("[DentalRay پشتیبان] انجام شد ← {Path}", RootPath);
            return (error is null, error);
        }
        finally
        {
            lock (Gate) _running = false;
        }
    }

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DbFolder);
        Directory.CreateDirectory(ImagesFolder);

        // ۱) دیتابیس
        string bak = Path.Combine(DbFolder, $"dentix_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
        // بدونِ رشتهٔ درون‌کاشته (EF1002)؛ مسیر فقط از کانفیگ می‌آید و ' هم گریخته شده.
        string backupSql = "BACKUP DATABASE [Dentix] TO DISK = '" + bak.Replace("'", "''") + "' WITH INIT, COMPRESSION";
        await _db.Database.ExecuteSqlRawAsync(backupSql, cancellationToken);

        // نگه‌داشتنِ فقط N نسخهٔ آخر
        var old = Directory.GetFiles(DbFolder, "dentix_*.bak")
            .OrderByDescending(File.GetLastWriteTime)
            .Skip(KeepBackups);
        foreach (var f in old) { try { File.Delete(f); } catch { } }

        // ۲) تصاویر: کپیِ تکمیلی بدونِ حذف (فایل‌ها تغییرناپذیرند؛ فقط اضافه می‌شوند)
        string source = _storage.GetRootPath();
        if (Directory.Exists(source))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "robocopy",
                Arguments = $"\"{source}\" \"{ImagesFolder}\" /E /XO /R:1 /W:1 /NP /NDL /NJH /NFL /BYTES",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                await proc.WaitForExitAsync(cancellationToken);
                // robocopy: 0 تا 7 یعنی موفق
                if (proc.ExitCode >= 8)
                    throw new InvalidOperationException($"robocopy با کد {proc.ExitCode} خطا داد.");
            }
        }
    }

    /// <summary>وضعیت برای نمایش در تنظیمات/داشبورد.</summary>
    public object GetStatus()
    {
        var state = ReadState();
        int dbCount = Directory.Exists(DbFolder) ? Directory.GetFiles(DbFolder, "dentix_*.bak").Length : 0;
        long imagesBytes = 0;
        if (Directory.Exists(ImagesFolder))
        {
            try
            {
                imagesBytes = Directory.EnumerateFiles(ImagesFolder, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }
            catch { /* دسترسی نامعتبر ⇒ صفر نشان می‌دهیم */ }
        }

        bool sameDrive = false;
        try
        {
            sameDrive = string.Equals(
                Path.GetPathRoot(RootPath)?.TrimEnd('\\'),
                Path.GetPathRoot(_storage.GetRootPath())?.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { /* مسیر نامعتبر */ }

        return new
        {
            rootPath = RootPath,
            keepBackups = KeepBackups,
            lastRun = state.LastRun,
            lastOk = state.Ok,
            lastError = state.Error,
            dbBackupCount = dbCount,
            imagesBytes,
            sameDriveWarning = sameDrive,
            due = IsDue(),
            running = IsRunning
        };
    }
}

/// <summary>ساعتِ پشتیبان: هر ساعت بررسی؛ اگر آخرین پشتیبان بیش از ۲۳ ساعت پیش بوده، اجرا.</summary>
public sealed class BackupScheduler : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BackupScheduler> _logger;

    public BackupScheduler(IServiceProvider services, ILogger<BackupScheduler> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // کمی تأخیر تا برنامه بالا بیاید و رابطِ کاربری آماده شود.
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var backup = scope.ServiceProvider.GetRequiredService<BackupService>();
                if (backup.IsDue())
                {
                    _logger.LogInformation("[DentalRay پشتیبان] شروعِ خودکار…");
                    await backup.RunAsync(stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[DentalRay پشتیبان] خطای زمان‌بند");
            }
            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); } catch { return; }
        }
    }
}