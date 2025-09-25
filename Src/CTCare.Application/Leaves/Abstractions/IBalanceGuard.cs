using CTCare.Domain.Entities;

namespace CTCare.Application.Leaves.Abstractions;

public interface IBalanceGuard
{
    /// <summary>
    /// Checks available and reserves Pending days inside the active transaction. Throws if insufficient.
    /// </summary>
    Task ReserveAsync(Guid employeeId, int year, decimal units, Guid? leaveTypeId, CancellationToken ct);

    /// <summary>
    /// Validates that approval can proceed: there are enough pending units reserved and
    /// the approval won't exceed entitlement. Throws if invalid.
    /// </summary>
    Task EnsureCanApproveAsync(LeaveBalance balance, decimal units, CancellationToken ct);
}
