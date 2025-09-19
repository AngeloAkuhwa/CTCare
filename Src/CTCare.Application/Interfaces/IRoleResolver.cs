using CTCare.Domain.Entities;

namespace CTCare.Application.Interfaces;

public interface IRoleResolver
{
    string[] Resolve(Employee employee);
}
