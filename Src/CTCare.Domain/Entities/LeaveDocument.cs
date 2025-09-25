using CTCare.Domain.Enums;
using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeaveDocument: BaseEntity
{
    public Guid LeaveRequestId { get; set; }
    public LeaveRequest LeaveRequest { get; set; } = default!;

    public DocumentKind Kind { get; set; }

    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long SizeBytes { get; set; }

    public string StoragePath { get; set; }

    public string? SecureUrl { get; set; }
    public string? ETag { get; set; }
    public string? Version { get; set; }
}
