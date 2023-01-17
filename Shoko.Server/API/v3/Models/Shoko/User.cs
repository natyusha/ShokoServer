using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Shoko;

public class User
{
    /// <summary>
    /// The UserID, this is used in a lot of v1 and v2 endpoints, and it's needed for editing or removing a user
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// Pretty Self-explanatory. It's the Username of the user
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Is the user an admin. Admins can perform all operations, including modification of users
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// This is a list of services that the user is set to use. AniDB, Trakt, and Plex, for example
    /// </summary>
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public List<CommunitySites> CommunitySites { get; set; }

    /// <summary>
    /// Restricted tags. Any group/series containing any of these tags will be
    /// rendered inaccessible to the user.
    /// </summary>
    public List<int> RestrictedTags { get; set; }

    public class FullUser : User
    {
        /// <summary>
        /// The password...Shoko is NOT secure, so don't assume this password is safe or even necessary to access the account
        /// </summary>
        public string Password { get; set; }

        public SVR_JMMUser GetServerModel()
        {
            var tags = RestrictedTags
                .Select(tagID => RepoFactory.AniDB_Tag.GetByTagID(tagID))
                .Where(tag => tag != null)
                .Select(tag => tag.TagName.ToLowerInvariant())
                .ToHashSet();
            var user = new SVR_JMMUser
            {
                Username = Username,
                JMMUserID = ID,
                Password = Digest.Hash(Password),
                RestrictedTags = tags,
                IsAdmin = IsAdmin,
                IsTraktUser = CommunitySites.Contains(global::Shoko.Models.Enums.CommunitySites.Trakt),
                IsAniDBUser = CommunitySites.Contains(global::Shoko.Models.Enums.CommunitySites.AniDB),
            };
            return user;
        }
    }

    public User() { }

    public User(SVR_JMMUser user)
    {
        ID = user.JMMUserID;
        Username = user.Username;
        IsAdmin = user.IsAdmin;
        CommunitySites = new List<CommunitySites>();
        if (user.IsAniDBUser)
        {
            CommunitySites.Add(global::Shoko.Models.Enums.CommunitySites.AniDB);
        }

        if (user.IsTraktUser)
        {
            CommunitySites.Add(global::Shoko.Models.Enums.CommunitySites.Trakt);
        }

        if (!string.IsNullOrEmpty(user.Plex.Token))
        {
            CommunitySites.Add(global::Shoko.Models.Enums.CommunitySites.Plex);
        }

        RestrictedTags = user.RestrictedTags
            .Select(name => RepoFactory.AniDB_Tag.GetByName(name).FirstOrDefault())
            .Where(tag => tag != null)
            .Select(tag => tag.TagID)
            .ToList();
    }

    public SVR_JMMUser MergeServerModel(SVR_JMMUser existing)
    {
        var tags = RestrictedTags
            .Select(tagID => RepoFactory.AniDB_Tag.GetByTagID(tagID))
            .Where(tag => tag != null)
            .Select(tag => tag.TagName.ToLowerInvariant())
            .ToHashSet();
        var user = new SVR_JMMUser
        {
            Username = Username,
            JMMUserID = ID,
            RestrictedTags = tags,
            IsAdmin = IsAdmin,
            IsTraktUser = CommunitySites.Contains(global::Shoko.Models.Enums.CommunitySites.Trakt),
            IsAniDBUser = CommunitySites.Contains(global::Shoko.Models.Enums.CommunitySites.AniDB),
            Password = existing?.Password ?? string.Empty,
        };
        return user;
    }

#nullable enable
    public class Input
    {
        public class ChangePasswordBody
        {
            public ChangePasswordBody()
            {
                Password = "";
                RevokeAPIKeys = true;
            }

            /// <summary>
            /// Password
            /// </summary>
            /// <value></value>
            [MaxLength(1024,
                ErrorMessage =
                    "Password cannot be longer than 1024 characters. Why the fuck do you need more than 1024 characters in your password?")]
            [Required(ErrorMessage = "Password is required", AllowEmptyStrings = true)]
            public string Password { get; set; }

            /// <summary>
            /// Revoke all previous active API keys for the user.
            /// </summary>
            /// <value></value>
            [DefaultValue(true)]
            public bool RevokeAPIKeys { get; set; }
        }
    }
}
