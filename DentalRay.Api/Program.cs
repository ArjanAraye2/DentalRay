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
builder.Services.AddHttpClient();
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
            const string loginStyle = "<link rel=\"stylesheet\" href=\"/css/login.css?v=20260920.4\" />";
            const string cardExtractionStyle = "<link rel=\"stylesheet\" href=\"/css/card-extraction.css?v=20260920.1\" />";
            html = html.Replace("</head>", $"{loginStyle}{Environment.NewLine}{cardExtractionStyle}{Environment.NewLine}</head>", StringComparison.OrdinalIgnoreCase);
            const string featureScripts =
                "<script src=\"/js/mobile-camera-loader.js?v=20260920.1\"></script>\n" +
                "<script src=\"/js/study-type-lookup.js?v=20260919.1\"></script>\n" +
                "<script src=\"/js/ai-study-analysis.js\"></script>\n" +
                "<script src=\"/js/card-extraction.js?v=20260919.2\"></script>\n" +
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
