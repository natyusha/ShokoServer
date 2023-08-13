using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.API;

public class UserHandler : AuthorizationHandler<UserHandler>, IAuthorizationRequirement
{
    private readonly Func<Shoko_User, bool> validationAction;

    public UserHandler(Func<Shoko_User, bool> validationAction)
    {
        this.validationAction = validationAction;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserHandler requirement)
    {
        if (context.User.GetUser() != null && requirement.validationAction(context.User.GetUser()))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
