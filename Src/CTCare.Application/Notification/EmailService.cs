using System.Text.RegularExpressions;

using CTCare.Application.Interfaces;
using CTCare.Shared;
using CTCare.Shared.Settings;

using Mailjet.Client;
using Mailjet.Client.Resources;

using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MimeKit;

using Newtonsoft.Json.Linq;

using Polly;

using RazorLight;
using RazorLight.Compilation;

namespace CTCare.Application.Notification;

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

    public async Task SendEmailAsync(string toAddress, string subject, string htmlBody, string plainTextBody = null!, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            throw new ArgumentException("toAddress is required", nameof(toAddress));
        }

        if (string.IsNullOrWhiteSpace(_settings.FromAddress))
        {
            throw new InvalidOperationException("EmailSettings.FromAddress is required.");
        }

        if (string.Equals(_settings.Provider, "Mailjet", StringComparison.OrdinalIgnoreCase))
        {
            await SendViaMailjetApiAsync(toAddress, subject, htmlBody, "plainTextBody", ct);
        }
        else
        {
            await SendViaSmtpAsync(toAddress, subject, htmlBody, plainTextBody, ct);
        }
    }
    private async Task SendViaSmtpAsync(
            string toAddress,
            string subject,
            string htmlBody,
            string plainTextBody = null!,
            CancellationToken ct = default)
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
            try
            {
                await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, ct);
                if (!string.IsNullOrWhiteSpace(_settings.Username))
                {
                    await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
                }

                await client.SendAsync(message, ct);
                _logger.LogInformation("Sent email to {To}", toAddress);
            }
            catch (SmtpCommandException sce)
            {
                _logger.LogError(sce, "SMTP command error ({StatusCode}) while sending to {To}", sce.StatusCode, toAddress);
                throw;
            }
            catch (SmtpProtocolException spe)
            {
                _logger.LogError(spe, "SMTP protocol error while sending to {To}", toAddress);
                throw;
            }
            finally
            {
                try { await client.DisconnectAsync(true, ct); } catch { /* ignore */ }
            }
        });
    }

    private async Task SendViaMailjetApiAsync(string to, string subject, string html, string? text, CancellationToken ct)
    {
        var apiKey = string.IsNullOrWhiteSpace(_settings.ApiKey) ? _settings.Username : _settings.ApiKey;
        var apiSecret = string.IsNullOrWhiteSpace(_settings.ApiSecret) ? _settings.Password : _settings.ApiSecret;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new InvalidOperationException("Mailjet API credentials are missing.");
        }

        await _retryPolicy.ExecuteAsync(async () =>
        {
            var client = new MailjetClient(apiKey, apiSecret);

            var req = new MailjetRequest { Resource = Send.Resource }
                .Property(Send.Messages, new JArray {
                    new JObject {
                        {"From", new JObject {
                            {"Email", _settings.FromAddress},
                            {"Name", _settings.FromName ?? string.Empty}
                        }},
                        {"To", new JArray {
                            new JObject {
                                {"Email", to},
                                {"Name", to}
                            }
                        }},
                        {"Subject", subject ?? string.Empty},
                        {"TextPart", text ?? StripHtml(html ?? string.Empty)},
                        {"HTMLPart", html ?? string.Empty}
                    }
                });

            var resp = await client.PostAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Mailjet API error: {Status} {Message} {Info} {Data}",
                    resp.StatusCode, resp.GetErrorMessage(), resp.GetErrorInfo(), resp.GetData());
                throw new InvalidOperationException($"Mailjet send failed: {resp.StatusCode}");
            }

            _logger.LogInformation("Mailjet: email sent to {To}. Count={Count}", to, resp.GetCount());
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
        try
        {

            if (_razorEngine == null)
            {
                throw new InvalidOperationException("Razor engine not configured.");
            }

            return await _razorEngine.CompileRenderAsync(templateKey, model);
        }
        catch (TemplateCompilationException tce)
        {
            _logger.LogError("Razor compile failed for '{Key}': {Errors}", templateKey, string.Join(Environment.NewLine, tce.CompilationErrors));
            throw;
        }
    }
}
