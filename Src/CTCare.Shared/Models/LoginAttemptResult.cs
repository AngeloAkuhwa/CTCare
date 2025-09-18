namespace CTCare.Shared.Models;

public sealed class LoginAttemptResult
{
    public bool IsLocked { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan? RemainingLockout { get; set; }
}
