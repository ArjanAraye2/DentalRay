using DentalRay.Api.Data;
using DentalRay.Api.Middleware;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "DentalRay";
});

string programDataPath =
    Environment.GetFolderPath(
        Environment.SpecialFolder.CommonApplicationData);

string dentalRayConfigFile =
    Path.Combine(
        programDataPath,
        "DentalRay",
        "DentalRay.config.json");

builder.Configuration.AddJsonFile(
    dentalRayConfigFile,
    optional: true,
    reloadOnChange: true);

builder.Services.AddControllers();

builder.Services.AddScoped<RadiologyStorageService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<StudyFinancialAuditService>();
builder.Services.AddScoped<ResourceAccessService>();

builder.Services.Configure<RadiologyStorageOptions>(
    builder.Configuration.GetSection("RadiologyStorage"));

builder.Services.AddDbContext<DentalRayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DentalRay")));

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "DentalRay.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin =
            context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            };

        options.Events.OnRedirectToAccessDenied =
            context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

    options.AddPolicy(
        "AdminOnly",
        policy =>
            policy.RequireRole("Admin"));
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<UserSessionValidationMiddleware>();
app.UseMiddleware<AuditMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
