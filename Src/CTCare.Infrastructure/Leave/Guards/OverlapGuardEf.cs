using CTCare.Application.Leaves.Abstractions;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Guards;

public sealed class OverlapGuardEf(CtCareDbContext db): IOverlapGuard
{
    public async Task EnsureNoOverlapAsync(
        Guid employeeId,
        DateOnly start,
        DateOnly end,
        Guid? excludeLeaveRequestId,
        CancellationToken ct)
    {
        // Check for overlaps against Approved or Submitted requests (inclusive range).
        var query = db.LeaveRequests
            .AsNoTracking()
            .Where(r =>
                r.EmployeeId == employeeId &&
                (r.Status == LeaveStatus.Approved || r.Status == LeaveStatus.Submitted) &&
                r.StartDate <= end && start <= r.EndDate);

        // When approving/editing an existing request, exclude that request itself.
        if (excludeLeaveRequestId.HasValue)
        {
            var idToExclude = excludeLeaveRequestId.Value;
            query = query.Where(r => r.Id != idToExclude);
        }

        var overlaps = await query.AnyAsync(ct);
        if (overlaps)
        {
            throw new InvalidOperationException("Request overlaps an existing leave (approved/submitted).");
        }
    }

}
