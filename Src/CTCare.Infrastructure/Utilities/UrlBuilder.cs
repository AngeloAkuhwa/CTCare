using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using CTCare.Shared.Settings;

namespace CTCare.Infrastructure.Utilities;

public static class UrlBuilder
{
    public static string Combine(params string?[] parts)
    {
        var cleaned = parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim().Trim('/'))
            .ToArray();

        if (cleaned.Length == 0)
        {
            return string.Empty;
        }

        // Preserve scheme://host if present in the first part
        if (cleaned[0].Contains("://", StringComparison.Ordinal))
        {
            return cleaned[0].TrimEnd('/') + "/" + string.Join("/", cleaned.Skip(1));
        }

        return string.Join("/", cleaned);
    }

    public static string WithQuery(string url, IDictionary<string, string?> query)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url ?? string.Empty;
        }

        var hasQuery = url.Contains('?', StringComparison.Ordinal);
        var prefix = hasQuery ? "&" : "?";

        var qs = string.Join("&", query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value != null)
            .Select(kv => $"{HttpUtility.UrlEncode(kv.Key)}={HttpUtility.UrlEncode(kv.Value)}"));

        return string.IsNullOrEmpty(qs) ? url : url + prefix + qs;
    }

    private static string BuildRootUrl(IHttpContextAccessor http, IOptions<AppSettings> app)
    {
        // Prefer explicit BaseUrl if given (treat it as *root*, we will add API paths below, idempotently)
        var cfg = (app.Value.BaseUrl ?? string.Empty).TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(cfg))
        {
            return TrimApiFrom(cfg); // in case someone put /api/v1 here, normalize back to root
        }

        var req = http.HttpContext?.Request;
        if (req == null)
        {
            return string.Empty;
        }

        var pathBase = req.PathBase.HasValue ? req.PathBase.Value.TrimEnd('/') : "";
        return $"{req.Scheme}://{req.Host.Value}{pathBase}".TrimEnd('/');
    }

    private static string BuildApiBase(IHttpContextAccessor http, IOptions<AppSettings> app, string apiVersion = "v1")
    {
        var cfg = (app.Value.BaseUrl ?? string.Empty).TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(cfg))
        {
            var lower = cfg.ToLowerInvariant();
            // If it already contains /api or /api/vX -> return as-is
            if (lower.Contains("/api/"))
            {
                return cfg;
            }

            // else append
            return Combine(cfg, "api", apiVersion);
        }

        // Build from the request root
        var root = BuildRootUrl(http, app);
        var lowerRoot = root.ToLowerInvariant();
        if (lowerRoot.Contains("/api/"))
        {
            return root; // unlikely, but safe
        }

        return Combine(root, "api", apiVersion);
    }

    public static string BuildAuthBase(IHttpContextAccessor http, IOptions<AppSettings> app, string apiVersion = "v1")
    {
        var apiBase = BuildApiBase(http, app, apiVersion).TrimEnd('/');

        if (apiBase.EndsWith("/auth", StringComparison.OrdinalIgnoreCase))
        {
            return apiBase;
        }

        return Combine(apiBase, "auth");
    }

    public static string BuildUiRoot(IOptions<AppSettings> app, IHttpContextAccessor? http = null)
    {
        return (app.Value.UIBaseUrl ?? string.Empty).TrimEnd('/');
    }

    private static string TrimApiFrom(string url)
    {
        // Normalize and split
        var lower = url.ToLowerInvariant();
        var idx = lower.IndexOf("/api/", StringComparison.Ordinal);
        if (idx < 0)
        {
            return url; // no /api in path
        }

        return url[..idx].TrimEnd('/');
    }
}
