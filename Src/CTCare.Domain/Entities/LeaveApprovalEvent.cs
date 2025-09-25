using CTCare.Domain.Enums;
using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeaveApprovalEvent: BaseEntity
{
    public Guid LeaveRequestId { get; set; }
    public LeaveRequest LeaveRequest { get; set; }

    public LeaveAction Action { get; set; }
    public Guid ActorEmployeeId { get; set; }
    public string? Note { get; set; }
}
