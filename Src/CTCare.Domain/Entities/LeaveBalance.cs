using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeaveBalance: BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public Guid? LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public int Year { get; set; }

    public decimal EntitledDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal PendingDays { get; set; }
}
