using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.AspNetCore.WebUtilities;

using Npgsql;

namespace CTCare.Api.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfirePostgres(this IServiceCollection services, IHostEnvironment env)
    {
        var sp = services.BuildServiceProvider();
        var config = sp.GetRequiredService<IConfiguration>();
        var conn = env.IsProduction() ? GetPgConnection(config) : config.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("Hangfire connection string is missing.");
        }

        var retries = 0;

        while (true)
        {
            try
            {
                services.AddHangfire(cfg =>
                    cfg.UseSimpleAssemblyNameTypeSerializer()
                        .UseRecommendedSerializerSettings()
                        .UsePostgreSqlStorage(conn));
                break;
            }
            catch when (retries++ < 5)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }


        services.AddHangfireServer();
        return services;
    }

    // ...

    private static string GetPgConnection(IConfiguration cfg)
    {
        var raw = cfg.GetConnectionString("DefaultConnection") ?? string.Empty;

        // Render (and many hosts) provide DATABASE URLs: postgres://user:pass@host:5432/db
        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw);

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

                // Render Postgres enforces TLS when you hit it over the public network.
                // If you use the internal connection on Render, you can relax this.
                SslMode = SslMode.Require,
                TrustServerCertificate = true
            };

            // carry across any query params on the URL (?pooling=true&timeout=15 etc.)
            var query = QueryHelpers.ParseQuery(uri.Query);
            foreach (var kvp in query)
            {
                // QueryHelpers returns values as StringValues; take the first
                var val = kvp.Value.Count > 0 ? kvp.Value[0] : null;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    b[kvp.Key] = val;
                }
            }

            raw = b.ToString();
        }

        return raw;
    }
}
