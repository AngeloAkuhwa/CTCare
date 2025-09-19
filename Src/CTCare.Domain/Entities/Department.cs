using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class Department: BaseEntity
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string? Description { get; set; }

    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
