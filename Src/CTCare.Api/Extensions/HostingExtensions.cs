namespace CTCare.Api.Extensions;

public static class HostingExtensions
{
    public static WebApplicationBuilder AddSentryMonitoring(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var env = builder.Environment;

        builder.WebHost.UseSentry(o =>
        {
            o.Dsn = config["Sentry:Dsn"] ?? config["SentryKey"];
            o.Debug = env.IsDevelopment();
            o.TracesSampleRate = 1.0;
            o.AutoSessionTracking = true;
            o.ServerName = env.ApplicationName;
            o.MinimumBreadcrumbLevel = LogLevel.Information;
            o.MinimumEventLevel = LogLevel.Error;
        });

        return builder;
    }
}
