using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IRelatedEntryMetadata
{
    #region Identifiers

    string? RelatedShowId { get; }

    string? RelatedMovieId { get; }

    #endregion

    #region Links

    IShowMetadata? RelatedShow { get; }

    IMovieMetadata? RelatedMovie { get; }

    #endregion

    #region Metadata

    RelationType RelationType { get; }

    #endregion
}
