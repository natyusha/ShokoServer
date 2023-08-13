using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Character : ICharacterMetadata
{
    #region Database columns

    public int AniDB_Anime_CharacterID { get; set; }
    public int AnimeID { get; set; }
    public int CharID { get; set; }
    public string CharType { get; set; }
    public string EpisodeListRaw { get; set; }

    #endregion
}

