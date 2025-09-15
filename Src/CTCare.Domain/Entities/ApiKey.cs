using CTCare.Domain.Primitives;

namespace CTCare.Domain.Entities;

public class ApiKey: BaseEntity
{
    public string Name { get; set; }
    public string Prefix { get; set; }
    public string Hash { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
