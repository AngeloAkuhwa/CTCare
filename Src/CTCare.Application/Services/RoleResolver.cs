using CTCare.Application.Interfaces;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;

namespace CTCare.Application.Services;

public sealed class RoleResolver: IRoleResolver
{
    public string[] Resolve(Employee emp)
    {
        var roles = new List<string> { nameof(UserRoles.Employee) };

        var dept = emp.Department?.Code;
        var title = emp.Designation;

        if (!string.IsNullOrWhiteSpace(dept) && dept.Equals("HR", StringComparison.OrdinalIgnoreCase))
        {
            roles.Add(nameof(UserRoles.HumanResourcePersonnel));
        }

        if (!string.IsNullOrWhiteSpace(dept) &&
            dept.Equals("ENG", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(title) &&
            title.Contains("Manager", StringComparison.OrdinalIgnoreCase))
        {
            roles.Add(nameof(UserRoles.EngineeringManager));
        }

        return roles.ToArray();
    }
}
