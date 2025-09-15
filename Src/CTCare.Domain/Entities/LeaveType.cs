using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;
public class LeaveType: BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int MaxDaysPerYear { get; set; }
    public bool RequiresDocumentAfterNConsecutiveDays { get; set; }
    public int? NConsecutiveDays { get; set; }
    public bool IsPaid { get; set; } = true;
}
