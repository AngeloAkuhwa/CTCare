using System.Data;
using System.Net;

using CTCare.Application.Interfaces;
using CTCare.Application.Leaves.Abstractions;
using CTCare.Application.Notification;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Utilities;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;
using CTCare.Shared.Utilities;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Leave.Commands;

public static class ApproveLeave
{
    public sealed class Command: IRequest<Result>
    {
        public Guid ManagerId { get; set; }
        public Guid LeaveRequestId { get; set; }
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public Guid LeaveRequestId { get; init; }
        public decimal Units { get; init; }
        public bool EmailSent { get; init; }
    }

    public sealed class Handler(
        CtCareDbContext db,
        IBusinessCalendarService calendar,
        ILeaveSpanCalculator span,
        IOverlapGuard overlap,
        IBalanceGuard balance,
        IEmailService email,
        IOptions<AppSettings> appSettings,
        ILogger<Handler> log,
        ICacheService? cache = null)
        : IRequestHandler<Command, Result>
    {
        private readonly IBusinessCalendarService _calendar = calendar;

        private const string EmailTemplate = "Templates.Email.LeaveApproved.cshtml";
        private const string EmailSubject = "Your leave request has been approved";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            // Load request + employee
            var lr = await db.LeaveRequests
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct);

            if (lr is null)
            {
                return new Result(HttpStatusCode.NotFound) { ErrorMessage = "Leave request not found." };
            }

            if (lr.Status != LeaveStatus.Submitted)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Only submitted requests can be approved." };
            }

            // Manager authorization (snapshot or current org manager)
            if (lr.ManagerId != req.ManagerId &&
                !(lr.Employee.ManagerId.HasValue && lr.Employee.ManagerId.Value == req.ManagerId))
            {
                return new Result(HttpStatusCode.Forbidden) { ErrorMessage = "You are not the manager for this request." };
            }

            // Recompute units & revalidate (idempotent)
            var units = span.ComputeUnits(lr.StartDate, lr.EndDate, lr.Unit);
            if (units <= 0)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Requested period has no working days." };
            }

            if (units != lr.DaysRequested)
            {
                // Keep stored snapshot consistent
                lr.DaysRequested = units;
            }

            await overlap.EnsureNoOverlapAsync(lr.EmployeeId, lr.StartDate, lr.EndDate, req.LeaveRequestId, ct);

            // Transaction: move Pending → Used
            var year = lr.StartDate.Year;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var lb = await db.LeaveBalances
                    .FirstOrDefaultAsync(x => x.EmployeeId == lr.EmployeeId
                                           && x.LeaveTypeId == lr.LeaveTypeId
                                           && x.Year == year, ct);

                if (lb is null)
                {
                    return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Leave balance not provisioned." };
                }

                // Double-check capacity (Pending already reserved on submit)
                await balance.EnsureCanApproveAsync(lb, units, ct);

                lb.PendingDays -= units;
                lb.UsedDays += units;

                lr.Status = LeaveStatus.Approved;
                lr.ManagerComment = null;

                db.LeaveApprovalEvents.Add(new LeaveApprovalEvent
                {
                    Id = SequentialGuid.NewGuid(),
                    LeaveRequestId = lr.Id,
                    Action = LeaveAction.Approved,
                    ActorEmployeeId = req.ManagerId,
                    Note = null
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(ct);
                log.LogWarning(ex, "Concurrency approving leave {LeaveRequestId}", lr.Id);
                return new Result(HttpStatusCode.Conflict) { ErrorMessage = "Conflict while approving. Please retry." };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                log.LogError(ex, "ApproveLeave failed for {LeaveRequestId}", lr.Id);
                return new Result(HttpStatusCode.InternalServerError) { ErrorMessage = "Internal error approving leave." };
            }

            // Cache bust (best-effort)
            try
            {
                if (cache is not null)
                {
                    await cache.RemoveAsync(CacheKeys.BalanceKey(lr.EmployeeId, year), ct);
                    await cache.InvalidateByTagAsync(CacheKeys.MyListPrefix(lr.EmployeeId), ct);
                    if (lr.ManagerId.HasValue)
                    {
                        await cache.InvalidateByTagAsync(CacheKeys.TeamListPrefix(lr.ManagerId.Value), ct);
                    }
                }
            }
            catch (Exception cex)
            {
                log.LogWarning(cex, "Cache invalidation failed after approving {LeaveRequestId}", lr.Id);
            }

            // Email notify (don’t fail approval if this throws)
            var emailSent = false;
            try
            {
                var portalBase = appSettings.Value.UIBaseUrl ?? appSettings.Value.BaseUrl ?? string.Empty;
                var detailsUrl = string.IsNullOrWhiteSpace(portalBase)
                    ? null
                    : UrlBuilder.Combine(portalBase, $"leave/requests/{lr.Id}");

                var html = await email.RenderTemplateAsync(EmailTemplate, new
                {
                    Name = $"{lr.Employee.FirstName} {lr.Employee.LastName}",
                    StartDate = lr.StartDate,
                    EndDate = lr.EndDate,
                    Units = lr.DaysRequested,
                    DetailsUrl = detailsUrl
                });

                await email.SendEmailAsync(lr.Employee.Email, EmailSubject, html, ct: ct);
                emailSent = true;
            }
            catch (Exception mailEx)
            {
                log.LogWarning(mailEx, "Approved leave {LeaveRequestId}, but failed to email {Email}", lr.Id, lr.Employee.Email);
            }

            return new Result(HttpStatusCode.OK)
            {
                LeaveRequestId = lr.Id,
                Units = units,
                EmailSent = emailSent
            };
        }
    }
}
