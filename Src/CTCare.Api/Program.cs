using CTCare.Api.Extensions;
using CTCare.Api.Filters;
using CTCare.Api.Middlewares;
using CTCare.Application.Files;
using CTCare.Application.Interfaces;
using CTCare.Application.Notification;
using CTCare.Application.Services;
using CTCare.Infrastructure.Files;
using CTCare.Infrastructure.Leave.Jobs;
using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Security;
using CTCare.Shared.Constants;
using CTCare.Shared.Settings;
using CTCare.Shared.SettingsValidator;

using Hangfire;

using Microsoft.Extensions.Options;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSentryMonitoring();
    builder.Services.AddControllers();
    builder.Services.AddAppSecurity();
    builder.Services.AddSwaggerWithJwtAndApiKey();

    var settings = builder.Configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
    builder.Services.AddCorsOpenPolicy(builder.Configuration, settings);

    builder.Services.AddDbContextAndHangfirePostgres(builder.Configuration, builder.Environment);
    builder.Services.AddRedisCaching(builder.Environment);
    builder.Services.AddHealthChecksInfra(builder.Environment);
    builder.Services.AddGlobalRateLimiting();
    builder.Services.AddApiKeyAuthOptions(builder.Configuration);

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblies(
            typeof(CTCare.Application.AssemblyMarker).Assembly,
            typeof(CTCare.Infrastructure.AssemblyMarker).Assembly
        )
    );

    builder.Services
        .AddOptions<LeaveRulesSettings>()
        .Bind(builder.Configuration.GetSection("LeaveRules"))
        .ValidateOnStart();

    builder.Services.Configure<LeaveRulesSettings>(builder.Configuration.GetSection("LeaveRules"));
    builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
    builder.Services.Configure<RedisSetting>(builder.Configuration.GetSection("RedisSetting"));
    builder.Services.AddSingleton(settings);
    builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
    builder.Services
        .Configure<EmailSetting>(builder.Configuration.GetSection("EmailSettings"))
        .AddSingleton<IEmailService, EmailService>();

    builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection(CloudinarySettings.SectionName));
    builder.Services.AddScoped<IFileStorage, CloudinaryFileStorage>();
    builder.Services.AddScoped<ICacheService, CacheService>();
    builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<IOtpService, OtpService>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IRoleResolver, RoleResolver>();
    builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
    builder.Services.RegisterLeaveServices();
    builder.Services.AddScoped<AnnualEntitlementProvisioner>();

    builder.Services.AddOptions<AuthSettings>()
        .BindConfiguration("Auth")
        .ValidateOnStart();

    builder.Services.AddOptions<AuthValidationLimits>()
        .BindConfiguration("AuthLimits")
        .ValidateOnStart();

    builder.Services.AddSingleton<IValidateOptions<AuthSettings>, AuthSettingsValidator>();

    // If running behind a proxy (Render), trust x-forwarded headers
    builder.Services.AddForwardedHeaders();


    var app = builder.Build();

    await app.Services.MigrateAsync(app.Environment, seed : DbSeed.SeedAsync);

    using (var scope = app.Services.CreateScope())
    {
        var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        manager.AddOrUpdate<AnnualEntitlementProvisioner>(
            "leave:provision:annual",
            svc => svc.ProvisionForYearAsync(
                DateTime.UtcNow.Year,
                Guid.Parse(LeaveTypeConstants.SickLeaveTypeConstant),
                CancellationToken.None),
            Cron.Yearly,
            TimeZoneInfo.Utc);
    }

    app.UseForwardedHeaders();
    app.UseSentryTracing();
    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        //HTTPS/HSTS early
        app.UseHttpsRedirection();
        app.UseHsts();
    }

    app.UseRouting();

    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "CTCare API v1");
        o.DisplayOperationId();
    });

    //CORS before auth/authorization
    app.UseCors(settings.PolicyName);

   app.UseApiKeyGate(builder.Configuration, app.Environment);
    //Auth => Rate limiting => Authorization
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    //Hangfire dashboard (after auth/authorization if you want it protected)
    // Optionally add an authorization filter if needed
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthFilter()]
    });

    app.MapHealthEndpoints();
   app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.InnerException);
    SentrySdk.CaptureException(ex);
    await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2));
}
