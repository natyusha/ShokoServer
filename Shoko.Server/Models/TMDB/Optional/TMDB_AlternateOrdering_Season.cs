using System;
using System.Linq;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_AlternateOrdering_Season : TMDB_Base<string>
{
    #region Properties

    public override string Id => TmdbEpisodeGroupID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrdering_SeasonID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Episode Group Collection ID.
    /// </summary>
    public string TmdbEpisodeGroupCollectionID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode Group ID.
    /// </summary>
    public string TmdbEpisodeGroupID { get; set; } = string.Empty;

    /// <summary>
    /// Episode Group Season name.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// Overridden season number for alternate ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Number of episodes within the alternate ordering season.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Indicates the alternate ordering season is locked.
    /// </summary>
    /// <remarks>
    /// Exactly what this 'locked' status indicates is yet to be determined.
    /// </remarks>
    public bool IsLocked { get; set; } = true;

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last syncronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion
    #region Constructors

    public TMDB_AlternateOrdering_Season() { }

    public TMDB_AlternateOrdering_Season(string episodeGroupId)
    {
        TmdbEpisodeGroupID = episodeGroupId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion
    #region Methods

    public bool Populate(TvGroup episodeGroup, string collectionId, int showId, int seasonNumber)
    {
        var updates = new[]
        {
            UpdateProperty(TmdbShowID, showId, v => TmdbShowID = v),
            UpdateProperty(TmdbEpisodeGroupCollectionID, collectionId, v => TmdbEpisodeGroupCollectionID = v),
            UpdateProperty(EnglishTitle, episodeGroup.Name, v => EnglishTitle = v),
            UpdateProperty(SeasonNumber, seasonNumber, v => SeasonNumber = v),
            UpdateProperty(EpisodeCount, episodeGroup.Episodes.Count, v => EpisodeCount = v),
            UpdateProperty(IsLocked, episodeGroup.Locked, v => IsLocked = v),
        };

        return updates.Any(updated => updated);
    }

    #endregion
}
