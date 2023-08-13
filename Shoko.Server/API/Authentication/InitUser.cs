using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Principal;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.API.Authentication;

// This is a fake user to aid in Authentication during first run
public class InitUser : Shoko_User, IIdentity
{
    public static InitUser Instance { get; } = new();

    private InitUser()
    {
        Id = 0;
        Username = "init";
        Password = "";
        IsAdmin = true;
        RestrictedTags = new();
    }

    [NotMapped] string IIdentity.AuthenticationType => "API";

    [NotMapped] bool IIdentity.IsAuthenticated => true;

    [NotMapped] string IIdentity.Name => Username;
}
