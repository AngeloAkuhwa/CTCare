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

namespace CTCare.Infrastructure.Leave.Commands;

public static class EditLeave
{
    public sealed class Command: IRequest<Result>
    {
        public Guid EmployeeId { get; set; }
        public Guid LeaveRequestId { get; set; }
        public Guid? LeaveTypeId { get; set; } 
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public LeaveUnit Unit { get; set; }
        public Guid? DoctorNoteAttachmentId { get; set; }
        public string? Comment { get; set; }
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public Guid LeaveRequestId { get; init; }
        public decimal Units { get; init; }
        public bool RequiresDoctorNote { get; init; }
    }

    public sealed class Handler(
        CtCareDbContext db,
        IBusinessCalendarService calendar,
        ILeaveSpanCalculator spanCalc,
        IDoctorsNoteRule docRule,
        IOverlapGuard overlapGuard,
        ICacheService cache)
        : IRequestHandler<Command, Result>
    {
        private const string MsgInvalidLeaveType = "Leave type is required.";
        private const string MsgInvalidDates = "End date must be on/after start date.";
        private const string MsgCrossYearNotAllowed = "Cross-year spans are not supported. Submit separate requests per year.";
        private const string MsgRequestNotFound = "Leave request not found.";
        private const string MsgNotOwner = "You can only edit your own leave requests.";
        private const string MsgOnlyReturnedEditable = "Only requests returned for correction can be edited.";
        private const string MsgNoWorkingDays = "Requested period has no working days.";
        private const string MsgDoctorsNoteRequired = "Doctor's note is required for requests over 2 consecutive business days.";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            if (req.LeaveTypeId is null)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgInvalidLeaveType };
            }

            if (req.EndDate < req.StartDate)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgInvalidDates };
            }

            if (req.StartDate.Year != req.EndDate.Year)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgCrossYearNotAllowed };
            }

            // Load the leave request and ensure ownership + status
            var lr = await db.LeaveRequests
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct);

            if (lr is null)
            {
                return new Result(HttpStatusCode.NotFound) { ErrorMessage = MsgRequestNotFound };
            }

            if (lr.EmployeeId != req.EmployeeId)
            {
                return new Result(HttpStatusCode.Forbidden) { ErrorMessage = MsgNotOwner };
            }

            if (lr.Status != LeaveStatus.Returned)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgOnlyReturnedEditable };
            }

            // Validate the (possibly changed) leave type exists
            var leaveTypeExists = await db.LeaveTypes
                .AsNoTracking()
                .AnyAsync(t => t.Id == req.LeaveTypeId.Value, ct);

            if (!leaveTypeExists)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgInvalidLeaveType };
            }

            decimal units;
            try
            {
                units = spanCalc.ComputeUnits(req.StartDate, req.EndDate, req.Unit);
            }
            catch (Exception ex)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = ex.Message };
            }

            if (units <= 0)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgNoWorkingDays };
            }

            // Doctor's note rule
            var requiresDoc = docRule.RequiresDoctorNote(req.StartDate, req.EndDate, req.Unit, calendar);
            if (requiresDoc && req.DoctorNoteAttachmentId is null)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgDoctorsNoteRequired };
            }

            // Overlap guard against APPROVED requests (and ignore current request)
            await overlapGuard.EnsureNoOverlapAsync(lr.EmployeeId, req.StartDate, req.EndDate, lr.Id, ct);

            // No balance reservation here. Just update the draft values on the Returned request.
            lr.LeaveTypeId = req.LeaveTypeId.Value;
            lr.StartDate = req.StartDate;
            lr.EndDate = req.EndDate;
            lr.Unit = req.Unit;
            lr.DaysRequested = units;
            lr.HasDoctorNote = req.DoctorNoteAttachmentId is not null;
            lr.DoctorNoteAttachmentId = req.DoctorNoteAttachmentId;
            lr.EmployeeComment = req.Comment;

            // Track an audit event for edit (optional but helpful)
            db.LeaveApprovalEvents.Add(new LeaveApprovalEvent
            {
                Id = SequentialGuid.NewGuid(),
                LeaveRequestId = lr.Id,
                Action = LeaveAction.Submitted, // or a custom "Edited" action if you have it
                ActorEmployeeId = req.EmployeeId,
                Note = "Request edited while Returned"
            });

            await db.SaveChangesAsync(ct);

            // Invalidate ONLY the employee's list cache; balance not touched here
            await cache.InvalidateByTagAsync(CacheKeys.MyListPrefix(req.EmployeeId), ct);

            return new Result(HttpStatusCode.OK)
            {
                LeaveRequestId = lr.Id,
                Units = units,
                RequiresDoctorNote = requiresDoc
            };
        }
    }
}
