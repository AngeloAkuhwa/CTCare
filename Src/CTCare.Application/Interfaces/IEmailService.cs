namespace CTCare.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string plainTextBody = null!,
        CancellationToken ct = default);
    Task<string> RenderTemplateAsync<TModel>(string templateKey, TModel model);
}
