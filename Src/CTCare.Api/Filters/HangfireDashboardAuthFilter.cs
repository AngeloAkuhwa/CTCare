using CTCare.Domain.Entities;
using CTCare.Domain.Enums;

using Hangfire.Dashboard;

namespace CTCare.Api.Filters;

public sealed class HangfireDashboardAuthFilter: IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext ctx)
    {
        var http = ctx.GetHttpContext();
        var env = http.RequestServices.GetRequiredService<IWebHostEnvironment>();

        // Open in Development
        if (env.IsDevelopment())
        {
            return true;
        }

        var user = http.User;
        return user?.Identity?.IsAuthenticated == true
               && (user.IsInRole(nameof(UserRoles.Employee)) || user.IsInRole(nameof(UserRoles.HumanResourcePersonnel)) ||
                   user.IsInRole(nameof(UserRoles.EngineeringManager)));
    }
}
