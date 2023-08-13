using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;

namespace Shoko.Server.Models.AniDB;
public class AniDB_ReleaseGroup : IReleaseGroup
{
    #region Database Columns

    public int AniDB_ReleaseGroupID { get; set; }

    public int GroupID { get; set; }

    public int Rating { get; set; }

    public int Votes { get; set; }

    public int AnimeCount { get; set; }

    public int FileCount { get; set; }

    public string Name { get; set; }

    public string ShortName { get; set; }

    public string IRCChannel { get; set; }

    public string IRCServer { get; set; }

    public string URL { get; set; }

    public string Picname { get; set; }

    #endregion

    #region IMetadata

    int IMetadata<int>.Id => GroupID;

    DataSource IMetadata.DataSource => DataSource.AniDB;

    #endregion
}
