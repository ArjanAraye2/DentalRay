using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.EntityFrameworkCore;

// ------------------------------------------------------------
// ایجاد Builder برنامه
// ------------------------------------------------------------
// WebApplication.CreateBuilder مسئول آماده‌سازی اولیه برنامه است.
//
// در این مرحله تنظیمات برنامه، Configuration، Logging و
// سرویس‌های موردنیاز ASP.NET Core آماده می‌شوند.
var builder = WebApplication.CreateBuilder(args);
// ============================================================
// Windows Service Support
// ============================================================
//
// این تنظیم باعث می‌شود DentalRay علاوه بر اجرای معمولی
// در Visual Studio یا Command Prompt، بتواند به صورت
// Windows Service نیز اجرا شود.
//
// در زمان نصب، Installer سرویس را ثبت خواهد کرد.
//
// ServiceName همان نامی است که در Windows Services
// نمایش داده می‌شود.
//
// ============================================================

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "DentalRay";
});
// ============================================================
// DentalRay External Configuration
// ============================================================
//
// تنظیمات قابل تغییر نسخه نصب‌شده را از ProgramData می‌خوانیم.
//
// دلیل استفاده از ProgramData:
//
// - فایل‌های Program Files معمولاً برای نوشتن نیاز به
//   Administrator دارند.
//
// - Installer می‌تواند تنظیمات را در ProgramData ایجاد کند.
//
// - مسیر تصاویر و Connection String بدون Compile مجدد
//   قابل تغییر خواهند بود.
//
// مسیر:
//
// C:\ProgramData\DentalRay\DentalRay.config.json
//
// ============================================================

string programDataPath =
    Environment.GetFolderPath(
        Environment.SpecialFolder.CommonApplicationData
    );


string dentalRayConfigDirectory =
    Path.Combine(
        programDataPath,
        "DentalRay"
    );


string dentalRayConfigFile =
    Path.Combine(
        dentalRayConfigDirectory,
        "DentalRay.config.json"
    );


// ------------------------------------------------------------
// اگر فایل Configuration خارجی وجود داشته باشد،
// بعد از appsettings.json خوانده می‌شود.
//
// بنابراین مقادیر آن روی appsettings.json اولویت دارند.
// ------------------------------------------------------------

builder.Configuration.AddJsonFile(
    dentalRayConfigFile,
    optional: true,
    reloadOnChange: true
);
// ------------------------------------------------------------
// ثبت سرویس‌های موردنیاز برنامه
// ------------------------------------------------------------

// اضافه کردن Controllerها به ASP.NET Core
//
// ما در DentalRay از Controllerها برای دریافت درخواست‌های
// HTTP مانند GET و POST استفاده می‌کنیم.
//
// مثال:
// GET  /api/patients/123
// POST /api/patients
builder.Services.AddControllers();
// ============================================================
// ثبت RadiologyStorageService در Dependency Injection
// ============================================================
//
// از این به بعد ASP.NET Core هر جا که به
// RadiologyStorageService نیاز داشته باشیم،
// می‌تواند یک نمونه از آن را ایجاد و در اختیارمان قرار دهد.
builder.Services.AddScoped<RadiologyStorageService>();
// ============================================================
// تنظیمات محل ذخیره تصاویر رادیولوژی
// ============================================================
//
// مقدار RootPath از بخش RadiologyStorage در appsettings.json
// خوانده می‌شود.
//
// مثال:
//
// "RadiologyStorage": {
//     "RootPath": "D:\\RadiologyData"
// }
//
// این مقدار بعداً هنگام نصب برنامه قابل تغییر خواهد بود.
//
builder.Services.Configure<RadiologyStorageOptions>(
    builder.Configuration.GetSection(
        "RadiologyStorage"
    )
);// ------------------------------------------------------------
// ثبت Entity Framework Core
// ------------------------------------------------------------
//
// این قسمت بسیار مهم است.
//
// به ASP.NET Core می‌گوییم که:
//
// "هر وقت برنامه به DentalRayDbContext نیاز داشت،
// یک DbContext آماده و متصل به SQL Server در اختیارش قرار بده."
//
// UseSqlServer مشخص می‌کند که دیتابیس ما SQL Server است.
//
// Connection String با نام "DentalRay" از فایل
// appsettings.json خوانده می‌شود.
builder.Services.AddDbContext<DentalRayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DentalRay")
    )
);


// ------------------------------------------------------------
// فعال کردن OpenAPI
// ------------------------------------------------------------
//
// OpenAPI اطلاعات مربوط به APIهای برنامه را تولید می‌کند.
//
// در آینده می‌توانیم از این اطلاعات برای مستندسازی و
// تست API استفاده کنیم.
builder.Services.AddOpenApi();


// ------------------------------------------------------------
// ساخت برنامه
// ------------------------------------------------------------
//
// تا اینجا سرویس‌های موردنیاز را تعریف کردیم.
// با Build برنامه واقعی ساخته می‌شود.
var app = builder.Build();


// ------------------------------------------------------------
// تنظیم Pipeline برنامه
// ------------------------------------------------------------
//
// Pipeline یعنی مسیر عبور درخواست‌های HTTP در برنامه.
//
// هر درخواست وارد برنامه می‌شود و از Middlewareهای مختلف
// عبور می‌کند تا در نهایت به Controller مربوطه برسد.


// فقط در محیط Development، اطلاعات OpenAPI را فعال می‌کنیم.
//
// در زمان توسعه برنامه این قابلیت برای آزمایش و بررسی API
// مفید است.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ============================================================
// Frontend Entry Page
// ============================================================
//
// فعلاً Study Delete در یک فایل JavaScript جدا نگهداری می‌شود
// تا قابلیت جدید بدون بازنویسی فایل بزرگ app.js قابل نگهداری باشد.
//
// برای صفحه اصلی، index.html را می‌خوانیم و قبل از </body>
// فایل study-delete.js را اضافه می‌کنیم. به این ترتیب app.js
// ابتدا بارگذاری می‌شود و سپس قابلیت Study Delete به همان UI
// موجود متصل می‌شود.
//
// مسیر /index.html نیز پوشش داده شده است تا رفتار هر دو آدرس
// یکسان باشد.
// ============================================================
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" ||
        context.Request.Path == "/index.html")
    {
        string indexPath =
            Path.Combine(
                app.Environment.WebRootPath,
                "index.html"
            );

        if (File.Exists(indexPath))
        {
            string html =
                await File.ReadAllTextAsync(indexPath);

            const string studyDeleteScript =
                "<script src=\"/js/study-delete.js\"></script>";

            html = html.Replace(
                "</body>",
                $"{studyDeleteScript}{Environment.NewLine}</body>",
                StringComparison.OrdinalIgnoreCase
            );

            context.Response.ContentType =
                "text/html; charset=utf-8";

            await context.Response.WriteAsync(html);
            return;
        }
    }

    await next();
});

// ------------------------------------------------------------
// Frontend Static Files
// ------------------------------------------------------------
//
// UseDefaultFiles باعث می‌شود اگر کاربر آدرس اصلی سایت را باز کرد:
//
// http://localhost:5202
//
// فایل index.html به صورت پیش‌فرض نمایش داده شود.
//
app.UseDefaultFiles();


// UseStaticFiles اجازه می‌دهد فایل‌های داخل پوشه wwwroot
// مانند HTML، CSS، JavaScript و تصاویر Frontend در دسترس باشند.
app.UseStaticFiles();

// ------------------------------------------------------------
// HTTPS Redirection
// ------------------------------------------------------------
//
// اگر درخواست با HTTP دریافت شود، برنامه می‌تواند آن را
// به HTTPS هدایت کند.
//
// فعلاً برای تست‌های ما از پروفایل HTTP استفاده می‌کنیم.
// بنابراین این خط را حذف نمی‌کنیم.
app.UseHttpsRedirection();


// ------------------------------------------------------------
// Authorization
// ------------------------------------------------------------
//
// این Middleware مربوط به کنترل مجوزهای دسترسی است.
//
// فعلاً در DentalRay سیستم Login و Authorization نداریم،
// اما این خط را نگه می‌داریم تا ساختار برنامه برای مراحل
// آینده آماده باشد.
app.UseAuthorization();


// ------------------------------------------------------------
// اتصال Controllerها به Pipeline
// ------------------------------------------------------------
//
// این دستور باعث می‌شود Routeهای تعریف‌شده در Controllerها
// فعال شوند.
//
// مثلاً:
//
// /api/test/database
// /api/patients/{nationalCode}
// /api/patients
//
app.MapControllers();


// ------------------------------------------------------------
// اجرای نهایی برنامه
// ------------------------------------------------------------
//
// از اینجا برنامه شروع به گوش دادن به درخواست‌های HTTP
// می‌کند و تا زمانی که برنامه متوقف نشده است، اجرا می‌شود.
app.Run();