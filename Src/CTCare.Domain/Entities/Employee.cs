using CTCare.Domain.Enums;
using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class Employee: BaseEntity
{
    public string EmployeeCode { get; set; }
    public EmploymentStatus Status { get; set; }
    public DateTimeOffset DateOfHire { get; set; }
    public decimal AnnualLeaveDays { get; set; }
    public decimal SickLeaveDays { get; set; }
    public decimal AnnualLeaveBalance { get; set; }
    public decimal SickLeaveBalance { get; set; }

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; }

    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }

    public string Designation { get; set; }
    public Gender Sex { get; set; }

    public string Email { get; set; } = default!;
    public EmailStatus EmailStatus { get; set; }

    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();

    //for backward compatibility just up-to latest previous team
    public string TeamLegacy { get; set; } = "";
    public string TeamCodeLegacy { get; set; } = "";
}
