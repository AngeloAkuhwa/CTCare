using System.Data;
using System.Net;

using CTCare.Application.Interfaces;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;
using CTCare.Shared.Utilities;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CTCare.Infrastructure.Leave.Commands;

public static class CancelLeaveByManager
{
    public sealed class Command: IRequest<BasicActionResult>
    {
        public Guid ManagerId { get; set; }
        public Guid LeaveRequestId { get; set; }
    }

    public sealed class Handler(CtCareDbContext db, ICacheService cache, ILogger<Handler> log)
        : IRequestHandler<Command, BasicActionResult>
    {

        private const string ErrNotFound = "Leave request not found.";
        private const string ErrForbidden = "You are not authorized to act on this request.";
        private const string ErrApprovedNotCancelable = "Only submitted or returned requests can be cancelled by a manager.";
        private const string NoteCancelledByManager = "Cancelled by manager.";

        public async Task<BasicActionResult> Handle(Command req, CancellationToken ct)
        {
            var lr = await db.LeaveRequests
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct);

            if (lr is null)
            {
                return new BasicActionResult(HttpStatusCode.NotFound) { ErrorMessage = ErrNotFound };
            }

            // RBAC: caller must be the snapshot manager OR the employee’s current manager
            var isManager =
                (lr.ManagerId.HasValue && lr.ManagerId.Value == req.ManagerId) ||
                (lr.Employee.ManagerId.HasValue && lr.Employee.ManagerId.Value == req.ManagerId);

            if (!isManager)
            {
                return new BasicActionResult(HttpStatusCode.Forbidden) { ErrorMessage = ErrForbidden };
            }

            // Only Submitted or Returned can be cancelled by manager
            if (lr.Status != LeaveStatus.Submitted && lr.Status != LeaveStatus.Returned)
            {
                return new BasicActionResult(HttpStatusCode.BadRequest) { ErrorMessage = ErrApprovedNotCancelable };
            }

            if (lr.Status == LeaveStatus.Submitted)
            {
                var lb = await db.LeaveBalances.FirstOrDefaultAsync(
                             x => x.EmployeeId == lr.EmployeeId && x.LeaveTypeId == lr.LeaveTypeId && x.Year == DateTimeOffset.UtcNow.Year, ct)
                         ?? throw new InvalidOperationException("Leave balance not found for the requested year/type.");

                if (lb.PendingDays < lr.DaysRequested)
                {
                    throw new InvalidOperationException($"Pending balance inconsistency. Pending={lb.PendingDays}, Requested={lr.DaysRequested}.");
                }

                lb.PendingDays -= lr.DaysRequested;
            }

            if (lr.Status == LeaveStatus.Cancelled)
            {
                return new BasicActionResult(HttpStatusCode.NoContent);
            }

            var year = lr.StartDate.Year;

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // If Submitted => release pending reservation
                if (lr.Status == LeaveStatus.Submitted)
                {
                    var lb = await db.LeaveBalances
                        .FirstOrDefaultAsync(x =>
                            x.EmployeeId == lr.EmployeeId &&
                            x.LeaveTypeId == lr.LeaveTypeId &&
                            x.Year == year, ct);

                    if (lb is null)
                    {
                        throw new InvalidOperationException("Leave balance not found for the requested year/type.");
                    }

                    // Be defensive against any drift
                    lb.PendingDays = Math.Max(0, lb.PendingDays - lr.DaysRequested);
                }

                lr.Status = LeaveStatus.Cancelled;
                lr.ManagerComment = NoteCancelledByManager;

                db.LeaveApprovalEvents.Add(new LeaveApprovalEvent
                {
                    Id = SequentialGuid.NewGuid(),
                    LeaveRequestId = lr.Id,
                    Action = LeaveAction.Cancelled,
                    ActorEmployeeId = req.ManagerId,
                    Note = NoteCancelledByManager
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                await cache.RemoveAsync(CacheKeys.BalanceKey(lr.EmployeeId, year), ct);
                await cache.InvalidateByTagAsync(CacheKeys.MyListPrefix(lr.EmployeeId), ct);
                await cache.InvalidateByTagAsync(CacheKeys.TeamListPrefix(lr.EmployeeId), ct);

                return new BasicActionResult(HttpStatusCode.NoContent);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(ct);
                log.LogWarning(ex, "Concurrency while manager-cancelling leave {LeaveRequestId}", lr.Id);
                return new BasicActionResult(HttpStatusCode.Conflict)
                {
                    ErrorMessage = "A conflict occurred while cancelling the request. Please retry."
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                log.LogError(ex, "Error while manager-cancelling leave {LeaveRequestId}", lr.Id);
                return new BasicActionResult(HttpStatusCode.InternalServerError)
                {
                    ErrorMessage = "Internal error cancelling the request."
                };
            }
        }
    }
}
