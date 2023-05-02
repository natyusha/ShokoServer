
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IVideoEpisodeCrossReference : IMetadata
{
    #region Identifiers

    int? VideoId { get; }

    int EpisodeId { get; }

    int SeriesId { get; }

    #endregion

    #region Links

    IShokoVideo? Video { get; }

    IShokoEpisode Episode { get; }

    IShokoSeries Series { get; }

    #endregion

    #region Metadata

    int Order { get; }

    decimal Percentage { get; }

    #endregion
}
