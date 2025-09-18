using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class Role: BaseEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = "";

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
