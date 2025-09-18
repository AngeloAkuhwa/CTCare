using CTCare.Shared.Settings;

using Microsoft.Extensions.Options;

namespace CTCare.Shared.SettingsValidator;

public sealed class AuthSettingsValidator(IOptions<AuthValidationLimits> limits): IValidateOptions<AuthSettings>
{
    private readonly AuthValidationLimits _l = limits.Value;

    public ValidateOptionsResult Validate(string? name, AuthSettings o)
    {
        var errors = new List<string>();

        if (o.OtpLength < _l.OtpLength.Min || o.OtpLength > _l.OtpLength.Max)
        {
            errors.Add($"OtpLength must be {_l.OtpLength.Min}-{_l.OtpLength.Max}.");
        }

        if (o.OtpExpiry < _l.OtpExpiry.Min || o.OtpExpiry > _l.OtpExpiry.Max)
        {
            errors.Add($"OtpExpiry must be between {_l.OtpExpiry.Min} and {_l.OtpExpiry.Max}.");
        }

        if (o.MaxFailedAttempts < _l.MaxFailedAttempts.Min || o.MaxFailedAttempts > _l.MaxFailedAttempts.Max)
        {
            errors.Add($"MaxFailedAttempts must be {_l.MaxFailedAttempts.Min}-{_l.MaxFailedAttempts.Max}.");
        }

        if (o.RetryWindow < _l.RetryWindowMin)
        {
            errors.Add($"RetryWindow must be ≥ {_l.RetryWindowMin}.");
        }

        if (o.LockoutDuration < _l.LockoutDurationMin)
        {
            errors.Add($"LockoutDuration must be ≥ {_l.LockoutDurationMin}.");
        }

        if (o.RefreshTokenValidityDays < _l.RefreshTokenValidityDays.Min || o.RefreshTokenValidityDays > _l.RefreshTokenValidityDays.Max)
        {
            errors.Add($"RefreshTokenValidityDays must be {_l.RefreshTokenValidityDays.Min}-{_l.RefreshTokenValidityDays.Max}.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
