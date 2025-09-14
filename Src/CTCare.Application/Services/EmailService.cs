using CTCare.Shared;

using CTCare.Shared.Settings;

namespace CTCare.Application.Services;
using System.Text.RegularExpressions;

using Interfaces;

using MailKit.Net.Smtp;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MimeKit;

using Polly;

using RazorLight;

public class EmailService: IEmailService
{
    private readonly EmailSetting _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly AsyncPolicy _retryPolicy;
    private readonly RazorLightEngine? _razorEngine;

    public EmailService(
        IOptions<EmailSetting> options,
        ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;

        // Build a simple exponential-backoff retry policy
        _retryPolicy = Polly.Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: _settings.RetryCount,
                sleepDurationProvider: i => TimeSpan.FromMilliseconds(_settings.RetryDelayMs * Math.Pow(2, i - 1)),
                onRetry: (ex, ts, attempt, ctx) =>
                    _logger.LogWarning(ex, "Email send attempt {Attempt} failed; retrying in {Delay}ms", attempt, ts.TotalMilliseconds)
            );

        // Razor templating (optional)
        _razorEngine = new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(EmailAssemblyMarker))
            .UseMemoryCachingProvider()
            .Build();
    }

    public async Task SendEmailAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string plainTextBody = null!)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            throw new ArgumentException("toAddress is required", nameof(toAddress));
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = plainTextBody ?? StripHtml(htmlBody)
        };
        message.Body = builder.ToMessageBody();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("Sent email to {To}", toAddress);
        });
    }

    /// <summary>Utility to strip HTML tags from the body for plain-text fallback.</summary>
    private static string StripHtml(string html)
    {
        // very simple; replace with a library if needed
        return Regex.Replace(html, "<.*?>", string.Empty);
    }

    /// <summary>
    /// Example method: render a Razor template from embedded resource.
    /// Template name might be \"Templates.Email.ConfirmAccount.cshtml\" in your assembly.
    /// </summary>
    public async Task<string> RenderTemplateAsync<TModel>(string templateKey, TModel model)
    {
        if (_razorEngine == null)
        {
            throw new InvalidOperationException("Razor engine not configured.");
        }

        return await _razorEngine.CompileRenderAsync(templateKey, model);
    }
}
