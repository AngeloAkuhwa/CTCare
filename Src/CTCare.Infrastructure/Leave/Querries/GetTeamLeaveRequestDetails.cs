using System.Net;

using CTCare.Application.Files;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Querries;

public static class GetTeamLeaveRequestDetails
{
    public sealed class ApprovalEventInfo
    {
        public string Action { get; set; }
        public Guid ActorEmployeeId { get; set; }
        public string ActorName { get; set; }
        public string? Note { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public sealed class TeamDetailsInfo: BasicActionResult
    {
        public TeamDetailsInfo(HttpStatusCode status) : base(status) { }
        public TeamDetailsInfo(string error) : base(error) { }

        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
        public Guid? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public Guid? LeaveTypeId { get; set; }
        public string? LeaveTypeName { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public LeaveUnit Unit { get; set; }
        public decimal DaysRequested { get; set; }
        public LeaveStatus Status { get; set; }

        public bool HasDoctorNote { get; set; }
        public Guid? DoctorNoteAttachmentId { get; set; }
        public string? DoctorNoteUrl { get; set; }

        public string? EmployeeComment { get; set; }
        public string? ManagerComment { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<ApprovalEventInfo> Events { get; set; }
    }

    public sealed class Query: IRequest<TeamDetailsInfo>
    {
        public Guid ManagerId { get; set; }
        public Guid LeaveRequestId { get; set; }
    }

    public sealed class Handler(CtCareDbContext db, IFileStorage fileStorage): IRequestHandler<Query, TeamDetailsInfo>
    {
        private const string ErrNotFound = "Leave request not found.";
        private const string ErrForbidden = "You are not authorized to view this request.";

        public async Task<TeamDetailsInfo> Handle(Query req, CancellationToken ct)
        {
            // Load the request with key relationships
            var lr = await db.LeaveRequests
                .Include(x => x.Employee)
                .Include(x => x.LeaveType)
                .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct);

            if (lr is null)
            {
                return new TeamDetailsInfo(HttpStatusCode.NotFound) { ErrorMessage = ErrNotFound };
            }

            // RBAC: caller must be the snapshot manager OR the employee’s current manager
            var isManager =
                (lr.ManagerId.HasValue && lr.ManagerId.Value == req.ManagerId) ||
                (lr.Employee.ManagerId.HasValue && lr.Employee.ManagerId.Value == req.ManagerId);

            if (!isManager)
            {
                return new TeamDetailsInfo(HttpStatusCode.Forbidden) { ErrorMessage = ErrForbidden };
            }

            // Project core details
            var detailsInfo = new TeamDetailsInfo(HttpStatusCode.OK)
            {
                Id = lr.Id,
                EmployeeId = lr.EmployeeId,
                EmployeeName = $"{lr.Employee.FirstName} {lr.Employee.LastName}",
                ManagerId = lr.ManagerId,
                ManagerName = await db.Employees
                    .Where(e => lr.ManagerId.HasValue && e.Id == lr.ManagerId.Value)
                    .Select(e => e.FirstName + " " + e.LastName)
                    .FirstOrDefaultAsync(ct),
                LeaveTypeId = lr.LeaveTypeId,
                LeaveTypeName = lr.LeaveType.Name,
                StartDate = lr.StartDate,
                EndDate = lr.EndDate,
                Unit = lr.Unit,
                DaysRequested = lr.DaysRequested,
                Status = lr.Status,
                HasDoctorNote = lr.HasDoctorNote,
                DoctorNoteAttachmentId = lr.DoctorNoteAttachmentId,
                EmployeeComment = lr.EmployeeComment,
                ManagerComment = lr.ManagerComment,
                CreatedAt = lr.CreatedAt
            };

            // Resolve doctor-note URL if we have a link service and an attachment
            if (detailsInfo is { HasDoctorNote: true, DoctorNoteAttachmentId: not null })
            {
                var doc = await db.LeaveDocuments.FirstAsync(x => x.Id == detailsInfo.DoctorNoteAttachmentId.Value && !x.IsDeleted, cancellationToken: ct);
                detailsInfo.DoctorNoteUrl = doc.SecureUrl;
            }

            // Audit trail
            detailsInfo.Events = await db.LeaveApprovalEvents
                .AsNoTracking()
                .Where(ev => ev.LeaveRequestId == lr.Id)
                .OrderBy(ev => ev.CreatedAt)
                .Select(ev => new ApprovalEventInfo
                {
                    Action = ev.Action.ToString(),
                    ActorEmployeeId = ev.ActorEmployeeId,
                    ActorName = db.Employees
                        .Where(e => e.Id == ev.ActorEmployeeId)
                        .Select(e => e.FirstName + " " + e.LastName)
                        .FirstOrDefault() ?? string.Empty,
                    Note = ev.Note,
                    Timestamp = ev.CreatedAt
                })
                .ToListAsync(ct);

            return detailsInfo;
        }
    }
}
