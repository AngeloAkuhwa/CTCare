namespace CTCare.Shared.Settings;
public class AuthSettings
{
    // OTP
    public int OtpLength { get; set; } = 6;
    public TimeSpan OtpExpiry { get; set; } = TimeSpan.FromMinutes(10);

    // Shared lockout policy
    public int MaxFailedAttempts { get; set; } = 5;
    public TimeSpan RetryWindow { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan PasswordResetTokenExpiryMinutes { get; set; }
    public int RefreshTokenValidityDays { get; set; }
}
