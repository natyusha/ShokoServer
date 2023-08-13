
#nullable enable
using System;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Base_User : IMetadata<int>
{
    #region Database Columns

    public int Id { get; set; }

    public int UserId { get; set; }

    #endregion

    #region Helpers

    public Shoko_User GetUser()
        => RepoFactory.Shoko_User.GetByID(UserId) ?? throw new NullReferenceException($"Shoko_User with Id {UserId} not found.");

    #endregion

    #region IMetadata

    public virtual DataSource DataSource => DataSource.Shoko;

    #endregion
}
