
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class SeriesCrossReference
{
    /// <summary>
    /// The provider id. Is a string because some providers use non-int ids.
    /// </summary>
    [Required]
    public string ID;

    /// <summary>
    /// The entry name for the provided cross-referenced entry.
    /// </summary>
    [Required]
    public string? Name;

    /// <summary>
    /// Provider season number, if appropriate.
    /// </summary>
    public int? SeasonNumber;

    /// <summary>
    /// The first episode number in the show or season, if appropriate.
    /// </summary>
    public int? StartEpisodeNumber;

    /// <summary>
    /// Provider type if the provider supports multiple different
    /// types of references, e.g. "Show" or "Movie" for TMDB, etc.
    /// </summary>
    [Required]
    public string? Type;

    /// <summary>
    /// Indicates this cross-reference cannot be edited or removed by the user.
    /// </summary>
    [Required]
    public bool IsLocked = false;

    /// <summary>
    /// The provider source, e.g. TvDB, TMDB, MAL, etc..
    /// </summary>
    [Required]
    public DataSource Source = DataSource.None;

    public SeriesCrossReference(CrossRef_AniDB_MAL xref)
    {
        ID = xref.MalAnimeId.ToString();
        IsLocked = true;
        Source = DataSource.MAL;
    }
    
    public SeriesCrossReference(CL_CrossRef_AniDB_TvDB xref)
    {
        ID = xref.TvDBID.ToString();
        Name = !string.IsNullOrEmpty(xref.TvDBTitle) ? xref.TvDBTitle : null;
        SeasonNumber = xref.TvDBSeasonNumber;
        StartEpisodeNumber = xref.TvDBStartEpisodeNumber;
        Source = DataSource.Trakt;
    }

    public SeriesCrossReference(CrossRef_AniDB_TraktV2 xref)
    {
        ID = xref.TraktID;
        Name = !string.IsNullOrEmpty(xref.TraktTitle) ? xref.TraktTitle : null;
        SeasonNumber = xref.TraktSeasonNumber;
        StartEpisodeNumber = xref.TraktStartEpisodeNumber;
        Source = DataSource.Trakt;
    }

    public SeriesCrossReference(CrossRef_AniDB_Other xref)
    {
        ID = xref.CrossRefID;
        if (xref.CrossRefType == (int)CrossRefType.MovieDB)
        {
            Type = "Movie";
            Source = DataSource.TMDB;
        }
    }

    public static class Input
    {
        public class AddCrossReferencesBody
        {
            /// <summary>
            /// The ids for the cross-references of the given <see cref="Type"/>
            /// to add.
            /// </summary>
            [Required]
            [MinLength(1)]
            public IReadOnlyList<string> IDs = new List<string>();

            /// <summary>
            /// The provider type for for the <see cref="IDs"/> to add.
            /// </summary>
            [MinLength(1)]
            public string? Type = null;

            /// <summary>
            /// Indicates all previous cross-references for the given
            /// <see cref="Type"/> and <see cref="Source"/> should be removed,
            /// and replaced by the new ids denoted in <see cref="IDs"/>
            /// /// </summary>
            public bool Replace = true;

            /// <summary>
            /// The data source of the cross-references to add.
            /// </summary>
            [Required]
            public DataSource Source = DataSource.None;
        }

        public class RemoveCrossReferencesBody
        {
            /// <summary>
            /// If provided then only the cross-references matching these ids
            /// for the given type will be removed.
            /// </summary>
            [MinLength(1)]
            public IReadOnlyList<string> IDs = new List<string>();

            /// <summary>
            /// If provided then only the cross-references for this given type
            /// will be removed.
            /// </summary>
            [MinLength(1)]
            public string? Type = null;

            /// <summary>
            /// The data source of the cross-reference(s) to remove.
            /// </summary>
            [Required]
            public DataSource Source = DataSource.None;
        }
    }
}
