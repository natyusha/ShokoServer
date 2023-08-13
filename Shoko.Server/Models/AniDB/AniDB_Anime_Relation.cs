
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Relation : IRelationMetadata
{
    #region Database columns

    public int Id { get; set; }
    public int AnidbAnimeId { get; set; }

    public string RawType { get; set; } = string.Empty;

    public int RelatedAnidbAnimeId { get; set; }

    public RelationType Type
    {
        get => RawType.ToLowerInvariant() switch
        {
            "prequel" => RelationType.Prequel,
            "sequel" => RelationType.Sequel,
            "parent story" => RelationType.MainStory,
            "side story" => RelationType.SideStory,
            "full story" => RelationType.FullStory,
            "summary" => RelationType.Summary,
            "other" => RelationType.Other,
            "alternative setting" => RelationType.AlternativeSetting,
            "alternative version" => RelationType.AlternativeVersion,
            "same setting" => RelationType.SameSetting,
            "character" => RelationType.SharedCharacters,
            _ => RelationType.Other,
        };
    }
    #endregion

    #region Helpers

    public AniDB_Anime GetAnime() =>
        RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AnidbAnimeId);

    public ShokoSeries? GetShokoSeries() =>
        RepoFactory.Shoko_Series.GetByAnidbAnimeId(AnidbAnimeId);

    public AniDB_Anime GetRelatedAnime() =>
        RepoFactory.AniDB_Anime.GetByAnidbAnimeId(RelatedAnidbAnimeId);

    public ShokoSeries? GetRelatedShokoSeries() =>
        RepoFactory.Shoko_Series.GetByAnidbAnimeId(RelatedAnidbAnimeId);

    #endregion

    #region IRelatedEntryMetadata

    string IMetadata<string>.Id =>
        Id.ToString();

    DataSource IMetadata.DataSource =>
        DataSource.AniDB;

    string IRelationMetadata.BaseId =>
        AnidbAnimeId.ToString();

    string IRelationMetadata.RelatedId =>
        RelatedAnidbAnimeId.ToString();

    IReadOnlyList<int> IRelationMetadata.BaseShokoSeriesIds
    {
        get
        {
            var series = GetShokoSeries();
            if (series == null)
                return new int[0] { };

            return new int[1] { series.Id };
        }
    }

    IReadOnlyList<int> IRelationMetadata.BaseShokoEpisodeIds =>
        new int[0] { };

    IReadOnlyList<int> IRelationMetadata.RelatedShokoSeriesIds
    {
        get
        {
            var series = GetRelatedShokoSeries();
            if (series == null)
                return new int[0] { };

            return new int[1] { series.Id };
        }
    }

    IReadOnlyList<int> IRelationMetadata.RelatedShokoEpisodeIds =>
        new int[0] { };

    IBaseMetadata IRelationMetadata.Base =>
        GetAnime();

    IReadOnlyList<IShokoSeries> IRelationMetadata.BaseShokoSeries
    {
        get
        {
            var series = GetShokoSeries();
            if (series == null)
                return new IShokoSeries[0] { };

            return new IShokoSeries[1] { series };
        }
    }

    IReadOnlyList<IShokoEpisode> IRelationMetadata.BaseShokoEpisodes =>
        new IShokoEpisode[0] {};

    IBaseMetadata? IRelationMetadata.Related =>
        GetRelatedAnime();

    IReadOnlyList<IShokoSeries> IRelationMetadata.RelatedShokoSeries
    {
        get
        {
            var series = GetRelatedShokoSeries();
            if (series == null)
                return new IShokoSeries[0] { };

            return new IShokoSeries[1] { series };
        }
    }

    IReadOnlyList<IShokoEpisode> IRelationMetadata.RelatedShokoEpisodes =>
        new IShokoEpisode[0] {};

    #endregion
}
