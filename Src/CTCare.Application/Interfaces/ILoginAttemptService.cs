using CTCare.Shared.Models;

namespace CTCare.Application.Interfaces;

public interface ILoginAttemptService
{
    Task<LoginAttemptResult> CheckStatusAsync(string failKey, string lockKey, CancellationToken ct = default);
    Task<LoginAttemptResult> RegisterFailureAsync(string failKey, string lockKey, CancellationToken ct = default);
    Task ResetAsync(CancellationToken ct = default, params string[]? keys);
}
