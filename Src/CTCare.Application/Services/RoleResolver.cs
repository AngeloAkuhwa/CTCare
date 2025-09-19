using CTCare.Application.Interfaces;
using CTCare.Domain.Entities;

namespace CTCare.Application.Services;

public sealed class RoleResolver: IRoleResolver
{
    public string[] Resolve(Employee emp)
    {
        var roles = new List<string> { "Employee" };

        var dept = emp.Department?.Code;
        var title = emp.Designation;

        if (!string.IsNullOrWhiteSpace(dept) &&
            dept.Equals("HR", StringComparison.OrdinalIgnoreCase))
        {
            roles.Add("HR");
        }

        if (!string.IsNullOrWhiteSpace(dept) &&
            dept.Equals("ENG", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(title) &&
            title.Contains("Manager", StringComparison.OrdinalIgnoreCase))
        {
            roles.Add("EngineeringManager");
        }

        return roles.ToArray();
    }
}
