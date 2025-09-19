using System.Text.Json;

using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;
public class AuditLog: BaseEntity
{
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; }
    public string Action { get; set; }// CreateLeaveRequest, ApproveStep, etc.
    public Guid EntityId { get; set; }
    public string EntityName { get; set; }
    public string[] ChangedColumns { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public JsonDocument? OldValues { get; set; }
    public JsonDocument? NewValues { get; set; }
    public string Metadata { get; set; }
}
