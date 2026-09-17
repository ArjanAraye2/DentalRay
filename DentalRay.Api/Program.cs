using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "DentalRay";
});

string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
string dentalRayConfigDirectory = Path.Combine(programDataPath, "DentalRay");
string dentalRayConfigFile = Path.Combine(dentalRayConfigDirectory, "DentalRay.config.json");
builder.Configuration.AddJsonFile(dentalRayConfigFile, optional: true, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddScoped<RadiologyStorageService>();

// HttpClientFactory is used by services such as runtime AI analysis.
// Sensitive API credentials stay on the DentalRay server and are never sent
// to the browser/mobile frontend.
builder.Services.AddHttpClient();

builder.Services.Configure<RadiologyStorageOptions>(builder.Configuration.GetSection("RadiologyStorage"));
builder.Services.AddDbContext<DentalRayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DentalRay")));
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

// ============================================================
// Frontend Entry Page
// ============================================================
// app.js is included by index.html. These small feature modules are loaded
// afterwards so newer features can be introduced incrementally while the
// original frontend remains usable during the DentalRay learning project.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" || context.Request.Path == "/index.html")
    {
        string indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
        if (File.Exists(indexPath))
        {
            string html = await File.ReadAllTextAsync(indexPath);
            const string featureScripts =
                "<script src=\"/js/study-delete.js\"></script>\n" +
                "<script src=\"/js/mobile-camera-loader.js\"></script>\n" +
                "<script src=\"/js/study-type-lookup.js\"></script>\n" +
                "<script src=\"/js/ai-study-analysis.js\"></script>";

            html = html.Replace(
                "</body>",
                $"{featureScripts}{Environment.NewLine}</body>",
                StringComparison.OrdinalIgnoreCase);

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
app.UseAuthorization();
app.MapControllers();
app.Run();
