using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class User: BaseEntity
{
    public Guid? EmployeeId { get; set; } 
    public Employee? Employee { get; set; }

    public string Email { get; set; }
    public bool EmailConfirmed { get; set; }

    public string PasswordHash { get; set; }
    public string PasswordSalt { get; set; }

    public bool TwoFactorEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEndUtc { get; set; }

    public string? LastLoginIp { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
