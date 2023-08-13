using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReferences;

/// <summary>
/// Stores a reference between the AniDB_File record and an AniDB_Episode
/// We store the Hash and the FileSize, because this is what enables us to make a call the AniDB UDP API using the FILE command
/// We store the AnimeID so we can download all the episodes from HTTP API (ie the HTTP api call will return the
/// anime details and all the episodes
/// Note 1 - A file can have one to many episodes, and an episode can have one to many files
/// Note 2 - By storing this information when a user manually associates a file with an episode, we can recover the manual
/// associations even when they move the files around
/// Note 3 - We can use a combination of the FileName/FileSize to determine the Hash for a file, this enables us to handle the
/// moving of files to different locations without needing to re-hash the file again
/// </summary>
public class CR_Video_Episode : IShokoVideoCrossReference
{
    #region Database Columns

    public int Id { get; set; }

    private string _ed2k { get; set; } = string.Empty;

    public string ED2K
    {
        get => _ed2k;
        set => _ed2k = value.ToUpperInvariant();
    }

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public int AnidbAnimeId { get; set; }

    public int AnidbEpisodeId { get; set; }

    public int? AnidbReleaseGroupId { get; set; }

    public int? CustomReleaseGroupId { get; set; }

    public int Order { get; set; }

    public int Percentage { get; set; }

    public CrossRefSource CrossReferenceSource { get; set; } = CrossRefSource.User;

    #endregion

    #region Helpers

    public AniDB_File AnidbFile =>
        RepoFactory.AniDB_File.GetByED2K(ED2K);

    public AniDB_Episode? AnidbEpisode =>
        RepoFactory.AniDB_Episode.GetByAnidbEpisodeId(AnidbEpisodeId);

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AnidbAnimeId);

    public AniDB_ReleaseGroup? AnidbReleaseGroup
    {
        get
        {
            if (!AnidbReleaseGroupId.HasValue)
                return null;

            var releaseGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(AnidbReleaseGroupId.Value);
            if (releaseGroup != null)
                return releaseGroup;

            return new() { GroupID = AnidbReleaseGroupId.Value };
        }
    }

    public Custom_ReleaseGroup? CustomReleaseGroup
    {
        get
        {
            if (!CustomReleaseGroupId.HasValue)
                return null;

            var releaseGroup = RepoFactory.Custom_ReleaseGroup.GetByID(CustomReleaseGroupId.Value);
            if (releaseGroup != null)
                return releaseGroup;

            return new() { Id = CustomReleaseGroupId.Value };
        }
    }

    public Shoko_Video? Video =>
        RepoFactory.Shoko_Video.GetByED2K(ED2K);

    public Shoko_Episode? Episode =>
        RepoFactory.Shoko_Episode.GetByAnidbEpisodeId(AnidbEpisodeId);


    public ShokoSeries? Series =>
        RepoFactory.Shoko_Series.GetByAnidbAnimeId(AnidbAnimeId);

    #endregion

    #region IVideoEpisodeCrossReference

    int IShokoVideoCrossReference.VideoId => Video!.Id;

    int IShokoVideoCrossReference.EpisodeId => Episode!.Id;

    int IShokoVideoCrossReference.SeriesId => Series!.Id;

    int? IShokoVideoCrossReference.AnidbReleaseGroupId => CustomReleaseGroupId.HasValue ? null : AnidbReleaseGroupId;

    int? IShokoVideoCrossReference.CustomReleaseGroupId => CustomReleaseGroupId;

    // We force the type since the cross-reference will only be available
    // through the abstraction if it is properly linked to both a video, an
    // episode, and a series.
    IShokoVideo IShokoVideoCrossReference.Video => Video!;

    // We force the type since the cross-reference will only be available
    // through the abstraction if it is properly linked to both a video, an
    // episode, and a series.
    IShokoEpisode IShokoVideoCrossReference.Episode => Episode!;

    // We force the type since the cross-reference will only be available
    // through the abstraction if it is properly linked to both a video, an
    // episode, and a series.
    IEpisodeMetadata IShokoVideoCrossReference.AnidbEpisode => AnidbEpisode!;

    // We force the type since the cross-reference will only be available
    // through the abstraction if it is properly linked to both a video, an
    // episode, and a series.
    IShokoSeries IShokoVideoCrossReference.Series => Series!;

    // We force the type since the cross-reference will only be available
    // through the abstraction if it is properly linked to both a video, an
    // episode, and a series.
    IShowMetadata IShokoVideoCrossReference.AnidbAnime => AnidbAnime!;

    IReleaseGroup? IShokoVideoCrossReference.ReleaseGroup => (IReleaseGroup?)CustomReleaseGroup ?? AnidbReleaseGroup;

    decimal IShokoVideoCrossReference.Percentage => Percentage / 100;

    DataSource IMetadata.DataSource => CrossReferenceSource switch { CrossRefSource.AniDB => DataSource.AniDB, _ => DataSource.User };

    #endregion
}
