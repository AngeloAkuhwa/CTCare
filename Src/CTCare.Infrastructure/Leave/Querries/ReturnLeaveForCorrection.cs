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

namespace CTCare.Infrastructure.Leave.Querries
{
    public static class ReturnLeaveForCorrection
    {
        public sealed class Command: IRequest<Result>
        {
            public Guid ManagerId { get; set; }
            public Guid LeaveRequestId { get; set; }
            public string Comment { get; set; }
        }

        public sealed class Result: BasicActionResult
        {
            public Result(HttpStatusCode status) : base(status) { }
            public Result(string error) : base(error) { }

            public bool SuccessFull { get; init; }
        }

        public sealed class Handler(CtCareDbContext db, ICacheService cache): IRequestHandler<Command, Result>
        {
            private const string ErrCommentRequired = "A comment is required when returning a request.";
            private const string ErrNotFound = "Leave request not found.";
            private const string ErrOnlySubmittedReturn = "Only submitted requests can be returned for correction.";
            private const string ErrUnauthorized = "You are not the manager for this request.";
            private const string ErrBalanceNotProvisioned = "Leave balance not provisioned.";

            public async Task<Result> Handle(Command req, CancellationToken ct)
            {
                if (string.IsNullOrWhiteSpace(req.Comment))
                {
                    throw new ArgumentException(ErrCommentRequired);
                }

                var lr = await db.LeaveRequests
                             .Include(x => x.Employee)
                             .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct)
                         ?? throw new KeyNotFoundException(ErrNotFound);

                if (lr.Status != LeaveStatus.Submitted)
                {
                    throw new InvalidOperationException(ErrOnlySubmittedReturn);
                }

                // Manager authorization: either snapshot ManagerId or current org manager
                if (lr.ManagerId != req.ManagerId && !(lr.Employee.ManagerId.HasValue && lr.Employee.ManagerId.Value == req.ManagerId))
                {
                    throw new UnauthorizedAccessException(ErrUnauthorized);
                }

                var year = lr.StartDate.Year;

                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    // Release reserved pending units
                    var lb = await db.LeaveBalances
                        .FirstOrDefaultAsync(x =>
                            x.EmployeeId == lr.EmployeeId &&
                            x.LeaveTypeId == lr.LeaveTypeId &&
                            x.Year == year, ct)
                        ?? throw new InvalidOperationException(ErrBalanceNotProvisioned);

                    // Prevent negative pending due to any prior corrections
                    lb.PendingDays = Math.Max(0, lb.PendingDays - lr.DaysRequested);

                    lr.Status = LeaveStatus.Returned;
                    lr.ManagerComment = req.Comment;

                    db.LeaveApprovalEvents.Add(new LeaveApprovalEvent
                    {
                        Id = SequentialGuid.NewGuid(),
                        LeaveRequestId = lr.Id,
                        Action = LeaveAction.Returned,
                        ActorEmployeeId = req.ManagerId,
                        Note = req.Comment
                    });

                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);

                    await cache.RemoveAsync(CacheKeys.BalanceKey(lr.EmployeeId, year), ct);
                    await cache.InvalidateByTagAsync(CacheKeys.MyListPrefix(lr.EmployeeId), ct);

                    return new Result(HttpStatusCode.OK)
                    {
                        SuccessFull = true
                    };
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
