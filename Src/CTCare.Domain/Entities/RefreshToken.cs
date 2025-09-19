using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;
public class RefreshToken: BaseEntity
{
    public Guid EmployeeId { get; set; }
    public string Token { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
