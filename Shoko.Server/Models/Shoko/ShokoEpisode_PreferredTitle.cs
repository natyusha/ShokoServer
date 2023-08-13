
using System;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ShokoEpisode_PreferredTitle : IMetadata<int>
{
    public int Id { get; set; }

    /// <summary>
    /// The local title id for the given data source.
    /// </summary>
    public int TitleId { get; }
    
    /// <summary>
    /// True if this title perference should override the language preference
    /// set in the settings.
    /// </summary>
    public bool IsGlobal { get; }

    public TextLanguage Language { get; set; }

    public DataSource DataSource { get; set; }

    public ITitle Title
    {
        get
        {
            ITitle? title = DataSource switch
            {
                DataSource.AniDB => RepoFactory.AniDB_Episode_Title.GetByID(TitleId),
                DataSource.Shoko => RepoFactory.ShokoEpisode_Title.GetByID(TitleId),
                DataSource.TvDB => RepoFactory.TvDBEpisode_Title.GetByID(TitleId),
                DataSource.TMDB => RepoFactory.TMDBEpisode_Title.GetByID(TitleId),
                _ => null,
            };
            if (title == null)
                throw new NullReferenceException($"Unable to find title for the given source {DataSource.ToString()} and id {TitleId.ToString()}");

            switch (title)
            {
                case AniDB_Episode_Title anidbTitle:
                    anidbTitle.IsPreferred = true;
                    break;
                case ShokoEpisode_Title shokoTitle:
                    shokoTitle.IsPreferred = true;
                    break;
                case TvDBEpisode_Title tvdbTitle:
                    tvdbTitle.IsPreferred = true;
                    break;
                case TMDBEpisode_Title tmdbTitle:
                    tmdbTitle.IsPreferred = true;
                    break;
                case TitleImpl impl:
                    impl.IsPreferred = true;
                    break;
            }
            return title;
        }
    }
}
