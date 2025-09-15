using CTCare.Api.Extensions;
using CTCare.Api.Middlewares;
using CTCare.Application.Interfaces;
using CTCare.Application.Services;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.Settings;

using Hangfire;

using Microsoft.AspNetCore.Authentication;


try
{
    var builder = WebApplication.CreateBuilder(args);

    // --- Services ---
    builder.AddSentryMonitoring();
    builder.Services.AddControllers();
    builder.Services.AddAppSecurity();
    builder.Services.AddSwaggerWithJwtAndApiKey();
    builder.Services.AddCorsOpenPolicy("AllowAll");

    builder.Services.AddDbContextAndHangfirePostgres(builder.Environment);
    builder.Services.AddRedisCaching(builder.Environment);
    builder.Services.AddHealthChecksInfra(builder.Environment);
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


    // Auto-migrate + optional seed
    await app.Services.MigrateAsync(app.Environment, seed: app.Environment.IsProduction() ? null : DbSeed.SeedAsync);

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

    //Swagger (always on)
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "CTCare API v1");
        o.DisplayOperationId();
        //o.DefaultModelsExpandDepth(-1);
    });

    //CORS before auth/authorization
    app.UseCors("AllowAll");

    //Auth => Rate limiting => Authorization
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
    app.MapControllers().RequireAuthorization(SecurityExtensions.PolicyJwtAndApi);

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.InnerException);
    SentrySdk.CaptureException(ex);
    await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2));
}
