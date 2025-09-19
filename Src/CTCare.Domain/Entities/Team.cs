using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

/// <summary>
/// Sub unit under a Department (e.g., Engineering => Hospice, Home Health, Home Care).
/// </summary>
public class Team: BaseEntity
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Description { get; set; }

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = default!;

    public ICollection<Employee> Members { get; set; } = new List<Employee>();
}
