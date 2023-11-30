using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Util;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Alternate Season and Episode ordering using TMDB's "Episode Group" feature.
/// Note: don't ask me why they called it that.
/// </summary>
public class TMDB_AlternateOrdering : TMDB_Base<string>
{
    #region Properties

    public override string Id => TmdbEpisodeGroupCollectionID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrderingID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Network ID.
    /// </summary>
    /// <remarks>
    /// It may be null if the group is not tied to a network.
    /// </remarks>
    public int? TmdbNetworkID { get; set; }

    /// <summary>
    /// TMDB Episode Group Collection ID.
    /// </summary>
    public string TmdbEpisodeGroupCollectionID { get; set; } = string.Empty;

    /// <summary>
    /// The name of the alternate ordering scheme.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// A short overview about what the scheme entails.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Number of episodes within the episode group.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of seasons within the episode group.
    /// </summary>
    public int SeasonCount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public AlternateOrderingType Type { get; set; }

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

    public TMDB_AlternateOrdering() { }

    public TMDB_AlternateOrdering(string collectionId)
    {
        TmdbEpisodeGroupCollectionID = collectionId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion
    #region Methods

    public bool Populate(TvGroupCollection collection, int showId)
    {
        var updates = new[]
        {
            UpdateProperty(TmdbShowID, showId, v => TmdbShowID = v),
            UpdateProperty(TmdbNetworkID, collection.Network?.Id, v => TmdbNetworkID = v),
            UpdateProperty(EnglishTitle, collection.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, collection.Description, v => EnglishOverview = v),
            UpdateProperty(EpisodeCount, collection.EpisodeCount, v => EpisodeCount = v),
            UpdateProperty(SeasonCount, collection.GroupCount, v => SeasonCount = v),
            UpdateProperty(Type, Enum.Parse<AlternateOrderingType>(collection.Type.ToString()), v => Type = v),
        };

        return updates.Any(updated => updated);
    }

    public TMDB_Show? GetTmdbShow() =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public IReadOnlyList<TMDB_AlternateOrdering_Season> GetTmdbAlternateOrderingSeasons() =>
        RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupCollectionID(TmdbEpisodeGroupCollectionID);

    public IReadOnlyList<TMDB_AlternateOrdering_Episode> GetTmdbAlternateOrderingEpisodes() =>
        RepoFactory.TMDB_AlternateOrdering_Episode.GetByTmdbEpisodeGroupCollectionID(TmdbEpisodeGroupCollectionID);

    #endregion
}
