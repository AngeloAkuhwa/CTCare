using System.Net;

using CTCare.Application.Files;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Utilities;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Commands
{
    public static class UploadLeaveDocument
    {
        public sealed class Command: IRequest<Result>
        {
            public Guid UploaderEmployeeId { get; set; }
            public Guid LeaveRequestId { get; set; }
            public DocumentKind Kind { get; set; }
            public Stream? Content { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public long Length { get; set; }
        }

        public sealed class UploadLeaveDocumentForm
        {
            public IFormFile File { get; set; }
        }

        public sealed class Result: BasicActionResult
        {
            public Result(HttpStatusCode status) : base(status) { }
            public Result(string error) : base(error) { }

            public Guid DocumentId { get; init; }
            public string Url { get; init; }
        }

        public sealed class Handler(CtCareDbContext db, IFileStorage storage): IRequestHandler<Command, Result>
        {
            private const string ErrNotFound = "Leave request not found.";
            private const string ErrForbidden = "You are not allowed to attach a document to this request.";
            private const string ErrNoFile = "No file content provided.";

            public async Task<Result> Handle(Command req, CancellationToken ct)
            {
                if (req.Content is null || req.Length <= 0)
                {
                    return new Result(HttpStatusCode.BadRequest) { ErrorMessage = ErrNoFile };
                }

                var lr = await db.LeaveRequests
                    .Include(x => x.Employee)
                    .FirstOrDefaultAsync(x => x.Id == req.LeaveRequestId, ct);

                if (lr is null)
                {
                    return new Result(HttpStatusCode.NotFound) { ErrorMessage = ErrNotFound };
                }

                var isOwner = lr.EmployeeId == req.UploaderEmployeeId;
                var isSnapshotManager = lr.ManagerId.HasValue && lr.ManagerId.Value == req.UploaderEmployeeId;
                var isCurrentManager = lr.Employee.ManagerId.HasValue && lr.Employee.ManagerId.Value == req.UploaderEmployeeId;

                if (!isOwner && !isSnapshotManager && !isCurrentManager)
                {
                    return new Result(HttpStatusCode.Forbidden) { ErrorMessage = ErrForbidden };
                }

                var upload = await storage.UploadAsync(req.Content, req.FileName, req.ContentType, req.Length, ct);

                var doc = new LeaveDocument
                {
                    Id = SequentialGuid.NewGuid(),
                    LeaveRequestId = lr.Id,
                    Kind = req.Kind,
                    FileName = upload.FileName,
                    ContentType = upload.ContentType,
                    StoragePath = upload.StoragePath,
                    SizeBytes = upload.SizeBytes,
                    SecureUrl = upload.SecureUrl,
                    Version = upload.Version,
                    ETag = upload.ETag
                };

                lr.DoctorNoteAttachmentId = doc.Id;
                lr.EmployeeId = req.UploaderEmployeeId;
                lr.ManagerId = lr.Employee.ManagerId ?? lr.ManagerId;
                lr.HasDoctorNote = true;
                lr.UpdatedAt = DateTimeOffset.UtcNow;
                lr.UpdatedBy = req.UploaderEmployeeId;

                db.LeaveDocuments.Add(doc);
                await db.SaveChangesAsync(ct);

                return new Result(HttpStatusCode.OK)
                {
                    DocumentId = doc.Id,
                    Url = upload.SecureUrl
                };
            }
        }
    }
}
