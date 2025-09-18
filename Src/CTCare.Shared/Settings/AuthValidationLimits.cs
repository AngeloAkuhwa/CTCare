namespace CTCare.Shared.Settings;
public sealed class AuthValidationLimits
{
    public RangeInt OtpLength { get; set; } = new(4, 8);
    public RangeTime OtpExpiry { get; set; } = new(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
    public RangeInt MaxFailedAttempts { get; set; } = new(3, 10);
    public TimeSpan RetryWindowMin { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan LockoutDurationMin { get; set; } = TimeSpan.FromMinutes(1);
    public RangeInt RefreshTokenValidityDays { get; set; } = new(1, 90);
}


public readonly record struct RangeInt(int Min, int Max);
public readonly record struct RangeTime(TimeSpan Min, TimeSpan Max);
