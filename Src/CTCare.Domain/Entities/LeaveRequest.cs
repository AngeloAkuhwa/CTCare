using CTCare.Domain.Enums;
using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class LeaveRequest: BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal DurationDays { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public string? RejectionReason { get; set; }
    public LeaveUnit Unit { get; set; }
    public decimal DaysRequested { get; set; }
    public bool HasDoctorNote { get; set; }
    public Guid? DoctorNoteAttachmentId { get; set; }
    public Guid? ManagerId { get; set; }
    public string? EmployeeComment { get; set; }
    public string? ManagerComment { get; set; }
    public Employee Employee { get; set; } = default!;
    public LeaveType LeaveType { get; set; } = default!;
    public ICollection<LeaveApprovalStep> ApprovalFlow { get; set; } = new List<LeaveApprovalStep>();
    public ICollection<LeaveDocument> Documents { get; set; } = new List<LeaveDocument>();
}
