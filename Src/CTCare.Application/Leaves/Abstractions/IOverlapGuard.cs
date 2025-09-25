namespace CTCare.Application.Leaves.Abstractions;

public interface IOverlapGuard
{
    Task EnsureNoOverlapAsync(Guid employeeId, DateOnly start, DateOnly end, Guid? excludeLeaveRequestId, CancellationToken ct);
}
