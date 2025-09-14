namespace CTCare.Application.Interfaces;
public interface IEmailService
{
    /// <summary>Sends an email (optionally using a Razor template).</summary>
    Task SendEmailAsync(
      string toAddress,
      string subject,
      string htmlBody,
      string plainTextBody = null!);
    Task<string> RenderTemplateAsync<TModel>(string templateKey, TModel model);
}

