using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.AspNetCore.WebUtilities;

using Npgsql;

namespace CTCare.Api.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfirePostgres(
        this IServiceCollection services,
        IHostEnvironment env)
    {
        var sp = services.BuildServiceProvider();
         var config = sp.GetRequiredService<IConfiguration>();
        var raw = config.GetConnectionString("DefaultConnection") ?? string.Empty;
        var conn = NeedsUrlConversion(raw) ? ConvertPostgresUrlToNpgsql(raw) : raw;

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("Hangfire connection string is missing.");
        }

        // Retry a few times on cold boot
        var retries = 0;
        while (true)
        {
            try
            {
                services.AddHangfire(cfg =>
                    cfg.UseSimpleAssemblyNameTypeSerializer()
                       .UseRecommendedSerializerSettings()
                       .UsePostgreSqlStorage(conn, new PostgreSqlStorageOptions
                       {
                           SchemaName = "hangfire",
                           PrepareSchemaIfNecessary = true,
                           QueuePollInterval = TimeSpan.FromSeconds(5)
                       }));

                services.AddHangfireServer();
                return services;
            }
            catch when (retries++ < 5)
            {
                Console.WriteLine($"[Hangfire] Waiting for Postgres… attempt {retries}/5");
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
    }

    private static bool NeedsUrlConversion(string raw)
        => raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static string ConvertPostgresUrlToNpgsql(string url)
    {
        var uri = new Uri(url);

        // user:pass
        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        var user = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? "");
        var pass = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "");

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = user,
            Password = pass,
            Database = uri.AbsolutePath.Trim('/'),

            // Public Render DB endpoints usually require TLS
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        };

        var query = QueryHelpers.ParseQuery(uri.Query);
        foreach (var kv in query)
        {
            var value = kv.Value.Count > 0 ? kv.Value[0] : null;
            if (!string.IsNullOrWhiteSpace(value))
            {
                b[kv.Key] = value;
            }
        }

        return b.ToString();
    }
}
