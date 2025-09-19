namespace CTCare.Shared.Settings;
public class AuthSettings
{
    // OTP
    public int OtpLength { get; set; } = 6;
    public TimeSpan OtpExpiry { get; set; }

    // Shared lockout policy
    public int MaxFailedAttempts { get; set; }
    public TimeSpan RetryWindow { get; set; }
    public TimeSpan LockoutDuration { get; set; }
    public TimeSpan PasswordResetTokenExpiryMinutes { get; set; }
    public int RefreshTokenValidityDays { get; set; }
    public TimeSpan EmailConfirmTokenExpiry { get; set; }
    public TimeSpan EmailConfirmResendCooldown { get; set; }

}
