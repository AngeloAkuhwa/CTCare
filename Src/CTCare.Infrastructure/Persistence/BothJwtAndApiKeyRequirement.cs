using Microsoft.AspNetCore.Authorization;

namespace CTCare.Infrastructure.Persistence;
public class BothJwtAndApiKeyRequirement: IAuthorizationRequirement { }

public class BothJwtAndApiKeyHandler: AuthorizationHandler<BothJwtAndApiKeyRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext ctx, BothJwtAndApiKeyRequirement req)
    {
        var hasJwt = ctx.User.Identities.Any(i => i is { AuthenticationType: "Bearer", IsAuthenticated: true });
        var hasApiKey = ctx.User.Identities.Any(i => i is { AuthenticationType: ApiKeyAuthOptions.DefaultScheme, IsAuthenticated: true });

        if (hasJwt && hasApiKey)
        {
            ctx.Succeed(req);
        }

        return Task.CompletedTask;
    }
}

