using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Describes relations between two series entries.
/// </summary>
public class SeriesRelation
{
    /// <summary>
    /// The IDs of the series.
    /// </summary>
    public RelationIDs IDs;

    /// <summary>
    /// The IDs of the related series.
    /// </summary>
    public RelationIDs RelatedIDs;

    /// <summary>
    /// The relation between <see cref="SeriesRelation.IDs"/> and <see cref="SeriesRelation.RelatedIDs"/>.
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public RelationType Type { get; set; }

    /// <summary>
    /// AniDB, etc.
    /// </summary>
    [Required]
    public string Source { get; set; }

    public SeriesRelation(HttpContext context, AniDB_Anime_Relation relation, ShokoSeries series = null,
        ShokoSeries relatedSeries = null)
    {
        if (series == null)
        {
            series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(relation.AnidbAnimeId);
        }

        if (relatedSeries == null)
        {
            relatedSeries = RepoFactory.Shoko_Series.GetByAnidbAnimeId(relation.RelatedAnidbAnimeId);
        }

        IDs = new RelationIDs { AniDB = relation.AnidbAnimeId, Shoko = series?.Id };
        RelatedIDs = new RelationIDs { AniDB = relation.RelatedAnidbAnimeId, Shoko = relatedSeries?.Id };
        Type = relation.Type;
        Source = "AniDB";
    }

    internal static RelationType GetRelationTypeFromAnidbRelationType(string anidbType)
    {
        return anidbType.ToLowerInvariant() switch
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

            _ => RelationType.Other
        };
    }

    /// <summary>
    /// Relation IDs.
    /// </summary>
    public class RelationIDs
    {
        /// <summary>
        /// The ID of the <see cref="Series"/> entry.
        /// </summary>
        public int? Shoko { get; set; }

        /// <summary>
        /// The ID of the <see cref="Series.AniDB"/> entry.
        /// </summary>
        public int? AniDB { get; set; }
    }
}

public static class RelationExtensions
{
    /// <summary>
    /// Reverse the relation.
    /// </summary>
    /// <param name="type">The relation to reverse.</param>
    /// <returns>The reversed relation.</returns>
    public static RelationType Reverse(this RelationType type)
    {
        return type switch
        {
            RelationType.Prequel => RelationType.Sequel,
            RelationType.Sequel => RelationType.Prequel,
            RelationType.MainStory => RelationType.SideStory,
            RelationType.SideStory => RelationType.MainStory,
            RelationType.FullStory => RelationType.Summary,
            RelationType.Summary => RelationType.FullStory,
            _ => type
        };
    }
}
