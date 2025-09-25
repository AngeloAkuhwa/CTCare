using CTCare.Shared.Interfaces;

namespace CTCare.Application.Leaves
{
    public sealed class TeamLeaveFilterRequest: IPagedRequest
    {
        public string? StatusesCsv { get; set; }
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public string? Search { get; set; }

        // IPagedRequest
        public int Page { get; set; } = 1;
        public int PageLength { get; set; } = 20;
    }

    public sealed class TeamLeaveItemInfo
    {
        public Guid RequestId { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeEmail { get; set; }

        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }

        public Guid? TeamId { get; set; }
        public string? TeamName { get; set; }

        public Guid? ManagerId { get; set; }
        public string? ManagerName { get; set; }

        public Guid? LeaveTypeId { get; set; }
        public string? LeaveTypeName { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal DaysRequested { get; set; }
        public string Unit { get; set; }
        public string Status { get; set; }
        public bool HasDoctorNote { get; set; }

        public string? EmployeeComment { get; set; }
        public string? ManagerComment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
