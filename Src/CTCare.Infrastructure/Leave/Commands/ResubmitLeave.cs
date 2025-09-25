using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net;

using CTCare.Application.Interfaces;
using CTCare.Application.Leaves.Abstractions;
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

public static class ResubmitLeave
{
    public sealed class Command: IRequest<BasicActionResult>
    {
        [Required]
        public Guid? EmployeeId { get; set; }
        [Required]
        public Guid? LeaveRequestId { get; set; }
        public Guid? DoctorNoteAttachmentId { get; set; }
        public string? Comment { get; set; }
    }

    public sealed class Handler(
        CtCareDbContext db,
        IBusinessCalendarService calendar,
        ILeaveSpanCalculator span,
        IOverlapGuard overlap,
        IBalanceGuard balance,
        ICacheService cache,
        ILogger<Handler> log)
        : IRequestHandler<Command, BasicActionResult>
    {
        private const string ErrNotFound = "Leave request not found.";
        private const string ErrOwnership = "You can only resubmit your own leave request.";
        private const string ErrState = "Only requests returned for correction can be resubmitted.";
        private const string ErrYearSpan = "Cross-year spans are not supported.";
        private const string ErrDocNote = "Doctor's note is required for requests over 2 consecutive business days.";
        private const string ErrInternal = "Internal error resubmitting leave.";
        private const string AuditNote = "Resubmitted by employee";

        public async Task<BasicActionResult> Handle(Command req, CancellationToken ct)
        {
            var lr = await db.LeaveRequests
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct);

            if (lr is null)
            {
                return new BasicActionResult(HttpStatusCode.NotFound) { ErrorMessage = ErrNotFound };
            }

            if (lr.EmployeeId != req.EmployeeId)
            {
                return new BasicActionResult(HttpStatusCode.Forbidden) { ErrorMessage = ErrOwnership };
            }

            if (lr.Status != LeaveStatus.Returned)
            {
                return new BasicActionResult(HttpStatusCode.BadRequest) { ErrorMessage = ErrState };
            }

            if (lr.StartDate.Year != lr.EndDate.Year)
            {
                return new BasicActionResult(HttpStatusCode.BadRequest) { ErrorMessage = ErrYearSpan };
            }

            // Recompute units (current snapshot on the request)
            decimal units;
            try
            {
                units = span.ComputeUnits(lr.StartDate, lr.EndDate, lr.Unit);
            }
            catch (Exception e)
            {
                return new BasicActionResult(HttpStatusCode.BadRequest) { ErrorMessage = e.Message };
            }

            if (units <= 0)
            {
                return new BasicActionResult(HttpStatusCode.BadRequest) { ErrorMessage = "Requested period has no working days." };
            }

            var needsNote = false;
            try
            {
                needsNote = calendar.CountBusinessDaysInclusive(lr.StartDate, lr.EndDate) > 2 && lr.Unit == LeaveUnit.FullDay;
            }
            catch
            {
                // fallback to calculator-only path if calendar throws
                needsNote = units > 2 && lr.Unit == LeaveUnit.FullDay;
            }

            if (needsNote)
            {
                // Prefer the newly supplied attachment id, else keep existing one
                lr.DoctorNoteAttachmentId = req.DoctorNoteAttachmentId ?? lr.DoctorNoteAttachmentId;
                lr.HasDoctorNote = lr.DoctorNoteAttachmentId is not null;

                if (!lr.HasDoctorNote)
                {
                    return new BasicActionResult(HttpStatusCode.BadRequest) { ErrorMessage = ErrDocNote };
                }
            }
            else
            {
                // If no note required, clear flags if previously set
                if (req.DoctorNoteAttachmentId is not null)
                {
                    lr.DoctorNoteAttachmentId = req.DoctorNoteAttachmentId;
                    lr.HasDoctorNote = true;
                }
            }

            // Overlap guard vs Approved to exclude the same request id
            try
            {
                await overlap.EnsureNoOverlapAsync(lr.EmployeeId, lr.StartDate, lr.EndDate, Guid.Empty, ct);
            }
            catch (Exception e)
            {
                return new BasicActionResult(HttpStatusCode.BadRequest) { ErrorMessage = e.Message };
            }

            var year = lr.StartDate.Year;

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Reserve again (Pending += units). If this request had previously been Submitted,
                // your Cancel/Return logic already released Pending, so this is a fresh reservation.
                await balance.ReserveAsync(lr.EmployeeId, year, units, lr.LeaveTypeId, ct);

                // Update snapshot + state
                lr.DaysRequested = units;
                lr.Status = LeaveStatus.Submitted;
                lr.EmployeeComment = string.IsNullOrWhiteSpace(req.Comment) ? lr.EmployeeComment : req.Comment;

                db.LeaveApprovalEvents.Add(new LeaveApprovalEvent
                {
                    Id = SequentialGuid.NewGuid(),
                    LeaveRequestId = lr.Id,
                    Action = LeaveAction.Submitted,
                    ActorEmployeeId = lr.EmployeeId,
                    Note = string.IsNullOrWhiteSpace(req.Comment) ? AuditNote : req.Comment
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Cache invalidations
                try
                {
                    await cache.RemoveAsync(CacheKeys.BalanceKey(lr.EmployeeId, year), ct);
                    await cache.InvalidateByTagAsync(CacheKeys.MyListPrefix(lr.EmployeeId), ct);
                }
                catch (Exception cacheEx)
                {
                    log.LogWarning(cacheEx, "Cache invalidation failed for resubmitted leave {LeaveId}", lr.Id);
                }

                return new BasicActionResult(HttpStatusCode.OK);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(ct);
                log.LogWarning(ex, "Concurrency error while resubmitting leave {LeaveRequestId}", lr.Id);
                return new BasicActionResult(HttpStatusCode.Conflict) { ErrorMessage = "Please retry: a concurrent update occurred." };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                log.LogError(ex, "ResubmitLeave failed for {EmployeeId}", req.EmployeeId);
                return new BasicActionResult(HttpStatusCode.InternalServerError) { ErrorMessage = ErrInternal };
            }
        }
    }
}
