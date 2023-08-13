using System;
using System.Collections.Generic;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ShokoSeries_User : Base_User, IMemoryCollectable
{
    #region Database Columns

    public int SeriesId { get; set; }

    public bool IsFavorite { get; set; }

    public int UnwatchedEpisodeCount { get; set; }

    public int HiddenUnwatchedEpisodeCount { get; set; }

    public int WatchedEpisodeCount { get; set; }

    public int WatchedCount { get; set; }

    [Obsolete("Remove when removing desktop/v1 support. Only use in v1.")]
    public int PlayedCount { get; set; }

    [Obsolete("Remove when removing desktop/v1 support. Only use in v1.")]
    public int StoppedCount { get; set; }

    public DateTime? WatchedDate { get; set; }

    public DateTime? LastEpisodeUpdate { get; set; }

    #endregion

    #region Constructors

    public ShokoSeries_User() { }

    public ShokoSeries_User(int userID, int seriesID)
    {
        UserId = userID;
        SeriesId = seriesID;
    }

    #endregion

    #region Helpers

    public ShokoSeries GetSeries()
    {
        var series = RepoFactory.Shoko_Series.GetByID(SeriesId);
        if (series == null)
            throw new NullReferenceException($"ShokoSeries with Id {SeriesId} not found.");

        return series;
    }

    public IReadOnlyList<ShokoEpisode_User> GetEpisodeRecords() =>
        RepoFactory.Shoko_Episode_User.GetByUserIDAndSeriesID(UserId, SeriesId);

    #endregion

    #region Plex Contract

    private DateTime _nextRegenAfter = DateTime.MinValue;

    private Video? _plexContract = null;

    public virtual Video? PlexContract
    {
        get
        {
            // If the contract is not null and the next regen is far off then return the currently cached contract.
            if (_plexContract != null && _nextRegenAfter > DateTime.Now)
                return _plexContract;

            // Otherwise try to update the contract.
            return UpdatePlexContract();
        }
    }

    public Video? UpdatePlexContract()
    {
        // Safely get the series, anime and user contract are available, otherwise abort.
        var series = RepoFactory.Shoko_Series.GetByID(SeriesId);
        var anime = series?.GetAnime();
        if (series == null || anime == null)
            return null;

        var userContract = series?.GetUserContract(UserId);
        if (userContract == null)
            return null;

        _nextRegenAfter = DateTime.Now.AddMinutes(10);
        return _plexContract = Helper.GenerateFromSeries(userContract, series, anime, UserId);
    }

    #endregion

    #region IMemoryCollectable

    public void CollectContractMemory()
    {
        _plexContract = null;
    }

    #endregion
}
