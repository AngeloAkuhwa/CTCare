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

public static class SubmitLeave
{
    public sealed class Command: IRequest<Result>
    {
        public Guid EmployeeId { get; set; }
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
        IBalanceGuard balanceGuard,
        ILogger<Handler> log,
        ICacheService cache
    ): IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            if (req.LeaveTypeId is null)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Leave type is required." };
            }

            if (req.EndDate < req.StartDate)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "End date must be on/after start date." };
            }

            // We keep balances per-year; disallow cross-year spans to avoid split logic here.
            if (req.StartDate.Year != req.EndDate.Year)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Cross-year spans are not supported. Submit separate requests per year." };
            }

            // Employee & leave type existence
            var employee = await db.Employees.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == req.EmployeeId, ct);
            if (employee is null)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Invalid employee." };
            }

            var leaveTypeExists = await db.LeaveTypes.AsNoTracking()
                .AnyAsync(t => t.Id == req.LeaveTypeId.Value, ct);
            if (!leaveTypeExists)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Invalid leave type." };
            }

            // Compute units (this should enforce half-day rules and business-day counting)
            decimal units;
            try
            {
                units = spanCalc.ComputeUnits(req.StartDate, req.EndDate, req.Unit);
            }
            catch (Exception e)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = e.Message };
            }

            if (units <= 0)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Requested period has no working days." };
            }

            // Doctor's note (>2 consecutive business days)
            var requiresDoc = docRule.RequiresDoctorNote(req.StartDate, req.EndDate, req.Unit, calendar);
            if (requiresDoc && req.DoctorNoteAttachmentId is null)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Doctor's note is required for requests over 2 consecutive business days." };
            }

            // Overlap with already approved (and optionally with submitted if your guard enforces it)
            try
            {
                await overlapGuard.EnsureNoOverlapAsync(req.EmployeeId, req.StartDate, req.EndDate, Guid.Empty,  ct);
            }
            catch (Exception e)
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = e.Message };
            }

            var year = req.StartDate.Year;

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Reserve from balance (Pending += units) atomically
                await balanceGuard.ReserveAsync(req.EmployeeId, year, units, req.LeaveTypeId, ct);

                var request = new LeaveRequest
                {
                    Id = SequentialGuid.NewGuid(),
                    EmployeeId = req.EmployeeId,
                    LeaveTypeId = req.LeaveTypeId.Value,
                    StartDate = req.StartDate,
                    EndDate = req.EndDate,
                    Unit = req.Unit,
                    DaysRequested = units,
                    HasDoctorNote = req.DoctorNoteAttachmentId is not null,
                    DoctorNoteAttachmentId = req.DoctorNoteAttachmentId,
                    Status = LeaveStatus.Submitted,
                    ManagerId = employee.ManagerId,
                    EmployeeComment = req.Comment,
                    CreatedBy = req.EmployeeId,
                    SubmittedAt = DateTimeOffset.UtcNow
                };

                db.LeaveRequests.Add(request);
                db.LeaveApprovalEvents.Add(new LeaveApprovalEvent
                {
                    Id = SequentialGuid.NewGuid(),
                    LeaveRequestId = request.Id,
                    Action = LeaveAction.Submitted,
                    ActorEmployeeId = req.EmployeeId,
                    Note = req.Comment,
                    LeaveRequest = request,
                    CreatedBy = req.EmployeeId
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Post-commit cache invalidation
                await cache.RemoveAsync(CacheKeys.BalanceKey(req.EmployeeId, year), ct);
                await cache.InvalidateByTagAsync(CacheKeys.MyListPrefix(req.EmployeeId), ct);

                return new Result(HttpStatusCode.Created)
                {
                    LeaveRequestId = request.Id,
                    Units = units,
                    RequiresDoctorNote = requiresDoc
                };
            }
            catch (DbUpdateConcurrencyException e)
            {
                await tx.RollbackAsync(ct);
                log.LogWarning(e, "Concurrency error while submitting leave for {EmployeeId}", req.EmployeeId);
                return new Result(HttpStatusCode.Conflict) { ErrorMessage = "Please retry: a concurrent update occurred." };
            }
            catch (Exception e)
            {
                await tx.RollbackAsync(ct);
                log.LogError(e, "SubmitLeave failed for {EmployeeId}", req.EmployeeId);
                return new Result(HttpStatusCode.InternalServerError) { ErrorMessage = "Internal error submitting leave." };
            }
        }
    }
}
