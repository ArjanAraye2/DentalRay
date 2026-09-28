using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using DentalRay.Api.Services.Pos;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService(options => { options.ServiceName = "DentalRay"; });

string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
string dentalRayConfigDirectory = Path.Combine(programDataPath, "DentalRay");
string dentalRayConfigFile = Path.Combine(dentalRayConfigDirectory, "DentalRay.config.json");
builder.Configuration.AddJsonFile(dentalRayConfigFile, optional: true, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddScoped<RadiologyStorageService>();
builder.Services.AddScoped<StudyAccessService>();
builder.Services.AddScoped<AiClient>();
// تبدیلِ PDF فقط روی ویندوز ممکن است (و برنامه هم ویندوزی است)؛ روی سیستمِ
// دیگر، سرویس ثبت نمی‌شود و آپلودِ PDF همان‌طور PDF می‌ماند.
if (OperatingSystem.IsWindows()) builder.Services.AddScoped<PdfToImageService>();
// POS terminals: the registry resolves the protocol named in the settings, so a
// new vendor only needs a new IPosProtocol implementation registered here.
builder.Services.AddSingleton<IPosProtocol, GenericTcpPosProtocol>();
builder.Services.AddSingleton<PosProtocolRegistry>();
// Patient messaging: sends SMS and records every attempt in tblPatientMessages.
builder.Services.AddScoped<PatientMessagingService>();
// Reading the radiology SMS that arrived on a paired phone: matching a message
// to a patient and importing the pictures its links point at.
builder.Services.AddScoped<SmsInboxService>();
// Central communication service: Kavenegar is the default SMS provider, while the provider remains configurable.
builder.Services.AddSingleton<ICommunicationService, CommunicationService>();
// Client for fetching images behind a share link. It calls back into this
// same server, so loopback must bypass any system proxy (see LoopbackBypassProxy).
builder.Services.AddHttpClient("ShareImages", client => client.Timeout = TimeSpan.FromSeconds(60))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseProxy = true,
        Proxy = new DentalRay.Api.Services.LoopbackBypassProxy()
    });
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// DentalRay is a browser application served by the same ASP.NET Core backend,
// therefore an HttpOnly authentication cookie is simpler and safer than storing
// a bearer token in browser storage. SameAsRequest keeps LAN development over
// HTTP working; deployed HTTPS automatically receives a Secure cookie.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "DentalRay.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Security is the default for every controller/action. An endpoint is public only
// when it explicitly declares [AllowAnonymous] (for example api/auth/login).
// This prevents a newly added API from accidentally being exposed on the clinic LAN.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.Configure<RadiologyStorageOptions>(builder.Configuration.GetSection("RadiologyStorage"));
// The connection string key used to be "DentalRay". Existing installations still
// carry that key in their config file, so both names are accepted; "Dentix" wins
// when present.
builder.Services.AddDbContext<DentalRayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Dentix")
        ?? builder.Configuration.GetConnectionString("DentalRay")));
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DentalRayDbContext>();
    try { await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('tblUsers', 'RecoveryMobile') IS NULL ALTER TABLE tblUsers ADD RecoveryMobile nvarchar(30) NULL"); } catch { }
    // نتیجهٔ تحلیلِ AI تصویر، تا تصویرِ بیمار فقط یک بار از مطب خارج شود و
    // بازدیدهای بعدی بدون هزینه و بدونِ ارسالِ دوباره انجام شود.
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.tblAIImageAnalyses', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.tblAIImageAnalyses (
                    AIImageAnalysisID bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblAIImageAnalyses PRIMARY KEY,
                    ImageID bigint NOT NULL,
                    Kind tinyint NOT NULL CONSTRAINT DF_tblAIImageAnalyses_Kind DEFAULT (1),
                    Model nvarchar(120) NOT NULL CONSTRAINT DF_tblAIImageAnalyses_Model DEFAULT (N''),
                    PromptVersion int NOT NULL CONSTRAINT DF_tblAIImageAnalyses_PromptVersion DEFAULT (0),
                    AnalysisJson nvarchar(max) NOT NULL,
                    AnalyzedAt datetime2 NOT NULL CONSTRAINT DF_tblAIImageAnalyses_AnalyzedAt DEFAULT (SYSUTCDATETIME()),
                    AnalyzedByUserID int NULL,
                    CONSTRAINT FK_tblAIImageAnalyses_RadiologyImages
                        FOREIGN KEY (ImageID) REFERENCES dbo.tblRadiologyImages (ImageID) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IX_tblAIImageAnalyses_ImageID_Kind
                    ON dbo.tblAIImageAnalyses (ImageID, Kind);
            END
            """);
    }
    catch { }

    // «کارت سابقه» نوعِ ثابتِ سند است: عکسِ کارتِ دستنویس که هنگامِ ثبتِ مراجعه
    // گرفته می‌شود، در گریدِ تصاویر دیده نمی‌شود و جزءِ شمارشِ تصاویر نیست.
    try
    {
        await db.Database.ExecuteSqlRawAsync(
            "IF NOT EXISTS (SELECT 1 FROM dbo.tblImageTypes WHERE ImageTypeName = N'کارت سابقه') " +
            "INSERT INTO dbo.tblImageTypes (ImageTypeName, IsActive) VALUES (N'کارت سابقه', 1)");
    }
    catch { }
}
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" || context.Request.Path == "/index.html")
    {
        string indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
        if (File.Exists(indexPath))
        {
            string html = await File.ReadAllTextAsync(indexPath);
            const string loginStyle = "<link rel=\"stylesheet\" href=\"/css/login.css?v=20260926.1\" />";
            const string cardExtractionStyle = "<link rel=\"stylesheet\" href=\"/css/card-extraction.css?v=20260920.1\" />";
            html = html.Replace("</head>", $"{loginStyle}{Environment.NewLine}{cardExtractionStyle}{Environment.NewLine}</head>", StringComparison.OrdinalIgnoreCase);
            const string featureScripts =
                "<script src=\"/js/mobile-camera-loader.js?v=20260920.1\"></script>\n" +
                "<script src=\"/js/study-type-lookup.js?v=20260919.1\"></script>\n" +
                "<script src=\"/js/ai-study-analysis.js?v=20260928.1\"></script>\n" +
                "<script src=\"/js/card-extraction.js?v=20260928.6\"></script>\n" +
                "<script src=\"/js/study-card-scan.js?v=20260928.1\"></script>\n" +
                "<script src=\"/js/dictation.js?v=20260928.2\"></script>\n" +
                "<script src=\"/js/login-ui.js?v=20260921.1\"></script>";
            html = html.Replace("</body>", $"{featureScripts}{Environment.NewLine}</body>", StringComparison.OrdinalIgnoreCase);
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
            return;
        }
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
