using CTCare.Domain.Enums;
using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeaveDocument: BaseEntity
{
    public Guid LeaveRequestId { get; set; }
    public LeaveRequest LeaveRequest { get; set; } = default!;

    public DocumentKind Kind { get; set; }

    // Client file info
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long SizeBytes { get; set; }

    // Legacy/local storage (CloudPublicId)
    public string StoragePath { get; set; }

    // Cloudinary first fields
    public string? SecureUrl { get; set; }
    public string? ETag { get; set; }
    public string? Version { get; set; }
}
