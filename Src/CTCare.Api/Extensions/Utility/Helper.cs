using System.Net;

namespace CTCare.Api.Extensions.Utility;
// Shared helper for BOTH app Db, Hangfire, and HealthChecks
public static class Helper
{
    public static string NormalizePostgresForRender(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        var isUrl = raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                    raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        if (!isUrl)
        {
            return raw;
        }

        var uri = new Uri(raw);

        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        var user = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? "");
        var pass = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "");

        var b = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = user,
            Password = pass,
            Database = uri.AbsolutePath.Trim('/'),
        };

        // If you are using the **external** DB URL, Render requires TLS.
        // Internal connections (the ones you get through "fromDatabase" or the
        // “Internal Database URL”) do NOT need TLS and may fail if you force it.
        var isExternal = !uri.Host.Contains("internal");     // crude but effective
        if (isExternal)
        {
            b.SslMode = Npgsql.SslMode.Require;
            b.TrustServerCertificate = true;
        }

        var q = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        foreach (var kv in q)
        {
            var val = kv.Value.Count > 0 ? kv.Value[0] : null;
            if (!string.IsNullOrWhiteSpace(val))
            {
                b[kv.Key] = val;
            }
        }

        return b.ToString();
    }

    public static (string endpoint, string password) NormalizeRedis(string cs, string configuredPassword)
    {
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("RedisSetting:ConnectionString is required.");
        }

        if (cs.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(cs);
            var host = uri.Host;
            var port = uri.IsDefaultPort ? 6379 : uri.Port;

            // user:pass (Render uses a generated token as "password")
            var parts = uri.UserInfo.Split(':', 2);
            var pwd = parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : configuredPassword;

            return ($"{host}:{port}", pwd ?? "");
        }

        // Already host:port or full option string
        return (cs, configuredPassword ?? "");
    }

    public static bool IsRunningInContainer() =>
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true", StringComparison.OrdinalIgnoreCase);
}
