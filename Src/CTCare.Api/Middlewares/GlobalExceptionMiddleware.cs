using System.Net;
using System.Text.Json;

namespace CTCare.Api.Middlewares;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHub sentry,
    IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            sentry.AddBreadcrumb(
                 message: "Incoming request",
                 category: "http",
                 data: new Dictionary<string, string>
                 {
                     ["method"] = context.Request.Method,
                     ["path"] = context.Request.Path.Value ?? "/"
                 }
             );


            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");

            // Enrich the Sentry scope
            sentry.ConfigureScope(scope =>
            {
                scope.SetTag("request_id", context.TraceIdentifier);
                scope.SetTag("route", context.GetEndpoint()?.DisplayName ?? "unknown");
                scope.SetExtra("query", context.Request.QueryString.Value);
                scope.SetExtra("user_agent", context.Request.Headers.UserAgent.ToString());

                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    scope.User = new SentryUser
                    {
                        Id = context.User.FindFirst("sub")?.Value ?? context.User.Identity?.Name,
                        Email = context.User.FindFirst("email")?.Value,
                        Username = context.User.Identity?.Name
                    };
                }
            });

            sentry.CaptureException(ex);

            await WriteProblemJsonAsync(context, ex);
        }
    }

    private static async Task WriteProblemJsonAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;


        var trace = new System.Diagnostics.StackTrace(exception, true);
        var frame = trace.GetFrames()?.FirstOrDefault(f => f.GetFileLineNumber() > 0);
        var method = frame?.GetMethod();
        var body = new
        {
            statusCode = context.Response.StatusCode,
            message = "An unexpected error occurred (development).",
            error = exception.Message,
            exceptionType = exception.GetType().Name,
            source = method?.DeclaringType?.FullName ?? "Unknown",
            method = method?.Name ?? "Unknown",
            file = frame?.GetFileName() ?? "N/A",
            line = frame?.GetFileLineNumber() ?? 0,
            stackTrace = exception.StackTrace,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
    }
}
