using CTCare.Domain.Enums;
using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeaveDocument: BaseEntity
{
    public Guid LeaveRequestId { get; set; }
    public DocumentKind Kind { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public string StoragePath { get; set; }
    public long SizeBytes { get; set; }
    public LeaveRequest LeaveRequest { get; set; }
}
