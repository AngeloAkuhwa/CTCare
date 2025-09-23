namespace CTCare.Shared.Settings;

public sealed class CorsSettings
{
    public string PolicyName { get; init; }
    public string[] AllowedOrigins { get; init; }
    public string[]? AllowedHeaders { get; init; }
    public string[]? AllowedMethods { get; init; }
    public int PreflightMaxAgeMinutes { get; init; }
    public bool AllowCredentials { get; init; }
}
