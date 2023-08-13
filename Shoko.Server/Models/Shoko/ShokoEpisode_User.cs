using System;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ShokoEpisode_User : Base_User
{
    #region Database Columns

    public int EpisodeId { get; set; }

    public int SeriesId { get; set; }

    public bool IsFavorite { get; set; }

    public int WatchedCount { get; set; }

    [Obsolete("Remove when removing desktop/v1 support. Only use in v1.")]
    public int PlayedCount { get; set; }

    [Obsolete("Remove when removing desktop/v1 support. Only use in v1.")]
    public int StoppedCount { get; set; }

    public DateTime? LastWatchedAt { get; set; }

    #endregion

    #region Constructors

    public ShokoEpisode_User() { }

    public ShokoEpisode_User(int userID, int episodeID, int seriesID)
    {
        UserId = userID;
        EpisodeId = episodeID;
        SeriesId = seriesID;
    }

    #endregion

    #region Helpers

    public Shoko_Episode GetEpisode()
    {
        var episode = RepoFactory.Shoko_Episode.GetByID(EpisodeId);
        if (episode == null)
            throw new NullReferenceException($"ShokoEpisode with Id {EpisodeId} not found.");

        return episode;
    }

    public ShokoSeries GetSeries()
    {
        var series = RepoFactory.Shoko_Series.GetByID(SeriesId);
        if (series == null)
            throw new NullReferenceException($"ShokoSeries with Id {SeriesId} not found.");

        return series;
    }

    #endregion
}
