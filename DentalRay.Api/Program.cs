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
builder.Services.Configure<RadiologyStorageOptions>(builder.Configuration.GetSection("RadiologyStorage"));
builder.Services.AddDbContext<DentalRayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DentalRay")));
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

// ============================================================
// Frontend Entry Page
// ============================================================
// These feature scripts are injected after app.js. AI results are runtime-only:
// ai-study-analysis.js renders the returned analysis in the browser and does not
// write it to DentalRay's database.
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
                "<script src=\"/js/ai-study-analysis.js\"></script>";
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
app.UseAuthorization();
app.MapControllers();
app.Run();