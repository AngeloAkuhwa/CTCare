namespace CTCare.Shared.Settings;
public class EmailSetting
{
    /// <summary>Smtp host, e.g. smtp.sendgrid.net or smtp.gmail.com</summary>
    public string SmtpHost { get; set; } = null!;

    /// <summary>Smtp port (587 for STARTTLS, 465 for SSL)</summary>
    public int SmtpPort { get; set; }

    /// <summary>Username for SMTP auth</summary>
    public string Username { get; set; } = null!;

    /// <summary>Password or API key</summary>
    public string Password { get; set; } = null!;

    /// <summary>From address (e.g. no-reply@CTCare.com)</summary>
    public string FromAddress { get; set; } = null!;

    /// <summary>From display name</summary>
    public string FromName { get; set; }

    /// <summary>Number of retry attempts on failure</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Base delay in milliseconds between retries</summary>
    public int RetryDelayMs { get; set; }
}
