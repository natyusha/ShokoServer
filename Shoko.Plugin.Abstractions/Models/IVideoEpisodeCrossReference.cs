using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Models;

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
