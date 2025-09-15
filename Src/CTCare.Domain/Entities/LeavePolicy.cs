using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeavePolicy: BaseEntity
{
    public Guid LeaveTypeId { get; set; }
    public decimal MaxDaysPerYear { get; set; }
    public bool RequiresManagerApproval { get; set; } = true;
    public bool RequiresHRApproval { get; set; }
    public int ApprovalSteps { get; set; }
    public LeaveType LeaveType { get; set; } = default!;
}
