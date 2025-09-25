// Src/CTCare.Infrastructure/Leave/Queries/GetMyLeaveRequestDetails.cs
using System.Net;
using CTCare.Domain.Enums;
using CTCare.Domain.Entities; // <= LeaveDocument lives here
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Querries
{
    public static class GetMyLeaveRequestDetails
    {
        public sealed class Query: IRequest<Result>
        {
            public Guid EmployeeId { get; set; }
            public Guid LeaveRequestId { get; set; }
        }

        public sealed class Result: BasicActionResult
        {
            public Result(HttpStatusCode status) : base(status) { }
            public LeaveRequestDetailsDto? Data { get; set; }
        }

        public sealed class LeaveRequestDetailsDto
        {
            public Guid Id { get; set; }
            public Guid EmployeeId { get; set; }
            public string EmployeeName { get; set; } = string.Empty;
            public Guid? LeaveTypeId { get; set; }
            public string? LeaveTypeName { get; set; }

            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
            public LeaveUnit Unit { get; set; }
            public decimal DaysRequested { get; set; }
            public LeaveStatus Status { get; set; }

            public string? EmployeeComment { get; set; }
            public string? ManagerComment { get; set; }

            public Guid? DoctorNoteAttachmentId { get; set; }
            public string? DoctorNoteUrl { get; set; }

            public DateTimeOffset CreatedAt { get; set; }
            public IReadOnlyList<ApprovalEventDto> Events { get; set; } = Array.Empty<ApprovalEventDto>();
        }

        public sealed class ApprovalEventDto
        {
            public string Action { get; set; } = string.Empty;
            public Guid ActorEmployeeId { get; set; }
            public string? Note { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
        }

        public sealed class Handler: IRequestHandler<Query, Result>
        {
            private const string ErrorNotFound = "Leave request not found.";
            private const string ErrorForbidden = "You are not allowed to view this leave request.";
            // Fallback (kept for backward-compat if a row lacks Cloudinary URL)
            private const string FilesApiPathPrefix = "/api/v1/files/";

            private readonly CtCareDbContext _db;

            public Handler(CtCareDbContext db)
            {
                _db = db ?? throw new ArgumentNullException(nameof(db));
            }

            public async Task<Result> Handle(Query req, CancellationToken ct)
            {
                var entity = await _db.LeaveRequests
                    .AsNoTracking()
                    .Include(x => x.Employee)
                    .Include(x => x.LeaveType)
                    .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct);

                if (entity is null)
                {
                    return new Result(HttpStatusCode.NotFound) { ErrorMessage = ErrorNotFound };
                }

                if (entity.EmployeeId != req.EmployeeId)
                {
                    return new Result(HttpStatusCode.Forbidden) { ErrorMessage = ErrorForbidden };
                }

                // Try to load the doctor’s note attachment row (by FK); if your schema also
                // labels kinds, you can add a Kind filter like:
                //   && d.Kind == LeaveDocumentKind.DoctorsNote
                LeaveDocument? doc = null;
                if (entity.DoctorNoteAttachmentId.HasValue)
                {
                    doc = await _db.LeaveDocuments
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d =>
                            d.Id == entity.DoctorNoteAttachmentId.Value
                            && d.LeaveRequestId == entity.Id, ct);
                }

                var events = await _db.LeaveApprovalEvents
                    .AsNoTracking()
                    .Where(e => e.LeaveRequestId == entity.Id)
                    .OrderBy(e => e.CreatedAt)
                    .Select(e => new ApprovalEventDto
                    {
                        Action = e.Action.ToString(),
                        ActorEmployeeId = e.ActorEmployeeId,
                        Note = e.Note,
                        CreatedAt = e.CreatedAt
                    })
                    .ToListAsync(ct);

                var details = new LeaveRequestDetailsDto
                {
                    Id = entity.Id,
                    EmployeeId = entity.EmployeeId,
                    EmployeeName = (entity.Employee.FirstName + " " + entity.Employee.LastName).Trim(),
                    LeaveTypeId = entity.LeaveTypeId,
                    LeaveTypeName = entity.LeaveType.Name,
                    StartDate = entity.StartDate,
                    EndDate = entity.EndDate,
                    Unit = entity.Unit,
                    DaysRequested = entity.DaysRequested,
                    Status = entity.Status,
                    EmployeeComment = entity.EmployeeComment,
                    ManagerComment = entity.ManagerComment,
                    DoctorNoteAttachmentId = entity.DoctorNoteAttachmentId,
                    DoctorNoteUrl = BuildDoctorNoteUrl(entity.DoctorNoteAttachmentId, doc),
                    CreatedAt = entity.CreatedAt,
                    Events = events
                };

                return new Result(HttpStatusCode.OK) { Data = details };
            }

            private static string? BuildDoctorNoteUrl(Guid? attachmentId, LeaveDocument? doc)
            {
                if (!attachmentId.HasValue)
                {
                    return null;
                }

                if (doc is not null && !string.IsNullOrWhiteSpace(doc.SecureUrl))
                {
                    return doc.SecureUrl;
                }

                // Fallback to legacy local files API route
                return FilesApiPathPrefix + attachmentId.Value.ToString("D");
            }
        }
    }
}
