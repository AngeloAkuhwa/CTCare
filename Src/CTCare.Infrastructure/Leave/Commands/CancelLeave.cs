using System.Data;

using CTCare.Application.Interfaces;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.Settings;
using CTCare.Shared.Utilities;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Commands
{
    public static class CancelLeave
    {
        public sealed class Command: IRequest<bool>
        {
            public Guid EmployeeId { get; set; }
            public Guid LeaveRequestId { get; set; }
        }

        public sealed class Handler(CtCareDbContext db, ICacheService? cache): IRequestHandler<Command, bool>
        {
            public async Task<bool> Handle(Command req, CancellationToken ct)
            {
                var lr = await db.LeaveRequests
                    .FirstOrDefaultAsync(x =>
                        x.Id == req.LeaveRequestId &&
                        x.EmployeeId == req.EmployeeId, ct)
                    ?? throw new KeyNotFoundException("Leave request not found.");

                if (lr.Status == LeaveStatus.Approved)
                {
                    throw new InvalidOperationException("Approved requests cannot be cancelled.");
                }

                if (lr.Status == LeaveStatus.Cancelled)
                {
                    return true;
                }

                var year = lr.StartDate.Year;

                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    // If it was Submitted we had a pending reservation; release it safely.
                    if (lr.Status == LeaveStatus.Submitted)
                    {
                        var lb = await db.LeaveBalances
                            .FirstOrDefaultAsync(x =>
                                x.EmployeeId == lr.EmployeeId &&
                                x.LeaveTypeId == lr.LeaveTypeId &&
                                x.Year == year, ct)
                            ?? throw new InvalidOperationException("Leave balance not found.");

                        // Prevent underflow due to any prior corrections
                        lb.PendingDays = Math.Max(0, lb.PendingDays - lr.DaysRequested);
                    }

                    // NB: If it was Returned (for correction) there is no pending reservation by design.
                    // Draft rarely exists in DB; if it does, cancelling is fine.

                    lr.Status = LeaveStatus.Cancelled;

                    db.LeaveApprovalEvents.Add(new LeaveApprovalEvent
                    {
                        Id = SequentialGuid.NewGuid(),
                        LeaveRequestId = lr.Id,
                        Action = LeaveAction.Cancelled,
                        ActorEmployeeId = req.EmployeeId,
                        Note = "Cancelled by employee"
                    });

                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);

                    if (cache is not null)
                    {
                        await cache.RemoveAsync(CacheKeys.BalanceKey(req.EmployeeId, year), ct);
                        await cache.InvalidateByTagAsync(CacheKeys.MyListPrefix(req.EmployeeId), ct);
                        await cache.InvalidateByTagAsync(CacheKeys.TeamListPrefix(req.EmployeeId), ct);
                    }

                    return true;
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }
        }
    }
}
