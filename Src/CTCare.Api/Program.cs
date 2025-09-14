using CTCare.Api.Extensions;
using CTCare.Api.Middlewares;
using CTCare.Application.Interfaces;
using CTCare.Application.Services;
using CTCare.Shared.Settings;

using Hangfire;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // --- Services ---
    builder.AddSentryMonitoring();
    builder.Services.AddControllers();
    builder.Services.AddSwaggerWithJwt();
    builder.Services.AddCorsOpenPolicy("AllowAll");

    builder.Services.AddHangfirePostgres();
    builder.Services.AddRedisCaching();
    builder.Services.AddHealthChecksInfra();
    builder.Services.AddGlobalRateLimiting();
    builder.Services.AddApiKeyAuthOptions();
    builder.Services
        .Configure<EmailSetting>(builder.Configuration.GetSection("EmailSettings"))
        .AddSingleton<IEmailService, EmailService>();
    builder.Services.AddScoped<ICacheService, CacheService>();


    // If running behind a proxy (Render), trust x-forwarded headers
    builder.Services.AddForwardedHeaders();

    var app = builder.Build();

    //Middleware pipeline

    //Forwarded headers FIRST (before anything that reads scheme/host)
    app.UseForwardedHeaders();

    //Sentry (request scope + tracing) and global exception handler
    app.UseSentryTracing();
    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        //HTTPS/HSTS early
        app.UseHttpsRedirection();
        app.UseHsts();
    }

    //Swagger (dev only)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    //CORS before auth/authorization
    app.UseCors("AllowAll");

    //Auth → Rate limiting → Authorization
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    //API key gate AFTER auth & before endpoints
    app.UseApiKeyGate();

    //Hangfire dashboard (after auth/authorization if you want it protected)
    // Optionally add an authorization filter if needed
    app.UseHangfireDashboard("/hangfire");

    //Health + Controllers
    app.MapHealthEndpoints();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    SentrySdk.CaptureException(ex);
    Console.WriteLine($"Fatal error: {ex}");
    await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2));
}
