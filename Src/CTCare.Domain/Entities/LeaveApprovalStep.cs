using CTCare.Domain.Enums;
using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeaveApprovalStep: BaseEntity
{
    public Guid LeaveRequestId { get; set; }
    public int StepNumber { get; set; }
    public Guid ApproverId { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? Comment { get; set; }
    public DateTimeOffset? ActedAt { get; set; }

    public Employee Approver { get; set; }
    public LeaveRequest LeaveRequest { get; set; }
}
