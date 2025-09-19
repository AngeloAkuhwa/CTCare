using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CTCare.Infrastructure.Persistence;

public class BothJwtAndApiKeyRequirement: IAuthorizationRequirement { }

public class BothJwtAndApiKeyHandler(IAuthenticationService auth, IHttpContextAccessor http)
    : AuthorizationHandler<BothJwtAndApiKeyRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BothJwtAndApiKeyRequirement requirement)
    {
        var http1 = http.HttpContext ?? context.Resource as HttpContext;
        if (http1 is null)
        {
            return;
        }

        var jwt = await auth.AuthenticateAsync(http1, JwtBearerDefaults.AuthenticationScheme);
        var api = await auth.AuthenticateAsync(http1, ApiKeyAuthOptions.DefaultScheme);

        if (jwt.Succeeded && api.Succeeded)
        {
            context.Succeed(requirement);
        }
    }
}
