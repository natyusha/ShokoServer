using System;
using System.Collections.Generic;
using System.Runtime;
using System.Threading.Tasks;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Settings;

// ReSharper disable InconsistentNaming

namespace Shoko.Server.Repositories;

public static class RepoFactory
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static readonly List<ICachedRepository> CachedRepositories = new();

    #region Direct

    public static AniDB_Anime_Character_Repository AniDB_Anime_Character { get; } = new();

    public static AniDB_Anime_DefaultImageRepository AniDB_Anime_DefaultImage { get; } = new();

    public static AniDB_Anime_RelationRepository AniDB_Anime_Relation { get; } = new();

    public static AniDB_Anime_SimilarRepository AniDB_Anime_Similar { get; } = new();

    public static AniDB_Anime_StaffRepository AniDB_Anime_Staff { get; } = new();

    public static AniDB_AnimeUpdateRepository AniDB_Anime_Update { get; } = new();

    public static AniDB_Character_SeiyuuRepository AniDB_Character_Creator { get; } = new();

    public static AniDB_CharacterRepository AniDB_Character { get; } = new();

    public static AniDB_FileUpdateRepository AniDB_File_Update { get; } = new();

    public static AniDB_GroupStatusRepository AniDB_Anime_ReleaseGroup_Status { get; } = new();

    public static AniDB_ReleaseGroupRepository AniDB_ReleaseGroup { get; } = new();

    public static AniDB_SeiyuuRepository AniDB_Creator { get; } = new();

    public static BookmarkedAnimeRepository ShokoSeries_Bookmark { get; } = new();

    public static CommandRequestRepository CommandRequest { get; } = new();

    public static CR_AniDB_TMDB_Movie_Repository CR_AniDB_TMDB_Movie { get; } = new();

    public static CR_AniDB_TMDB_Show_Repository CR_AniDB_TMDB_Show { get; } = new();

    public static CrossRef_AniDB_MALRepository CR_AniDB_MAL { get; } = new();

    public static CrossRef_AniDB_OtherRepository CR_AniDB_Other { get; } = new();

    public static CrossRef_Languages_AniDB_FileRepository CR_AniDB_File_Languages { get; } = new();

    public static CrossRef_Subtitles_AniDB_FileRepository CR_AniDB_File_Subtitles { get; } = new();

    public static FileNameHashRepository CR_FileName_ED2K { get; } = new();

    public static IgnoreAnimeRepository ShokoSeries_Ignore { get; } = new();

    public static RenameScriptRepository RenameScript { get; } = new();

    public static ScanFileRepository ScanFile { get; } = new();

    public static ScanRepository Scan { get; } = new();

    public static ScheduledUpdateRepository ScheduledUpdate { get; } = new();

    public static TMDB_Movie_Repository TMDB_Movie { get; } = new();

    public static TMDB_Show_Repository TMDB_Show { get; } = new();

    public static Trakt_EpisodeRepository Trakt_Episode { get; } = new();

    public static Trakt_SeasonRepository Trakt_Season { get; } = new();

    public static Trakt_ShowRepository Trakt_Show { get; } = new();

    public static VersionsRepository Versions { get; } = new();

    #endregion

    #region Cached
    // DECLARE THESE IN ORDER OF DEPENDENCY

    public static Shoko_User_Repository Shoko_User { get; } = new();

    public static AuthTokensRepository Shoko_User_AuthToken { get; } = new();

    public static ImportFolderRepository ImportFolder { get; } = new();

    public static AniDB_AnimeRepository AniDB_Anime { get; } = new();

    public static AniDB_Episode_TitleRepository AniDB_Episode_Title { get; } = new();

    public static AniDB_EpisodeRepository AniDB_Episode { get; } = new();

    public static AniDB_FileRepository AniDB_File { get; } = new();

    public static AniDB_Anime_TitleRepository AniDB_Anime_Title { get; } = new();

    public static AniDB_Anime_TagRepository AniDB_Anime_Tag { get; } = new();

    public static AniDB_TagRepository AniDB_Tag { get; } = new();

    public static Custom_ReleaseGroup_Repository Custom_ReleaseGroup { get; } = new();
    public static Custom_Tag_Repository Custom_Tag { get; } = new();

    public static CrossRef_CustomTagRepository CR_CustomTag { get; } = new();

    public static CR_Video_Episode_Repository CR_Video_Episode { get; } = new();

    public static ShokoVideoLocation_Repository Shoko_Video_Location { get; } = new();

    public static Shoko_Video_Repository Shoko_Video { get; } = new();

    public static Shoko_Video_User_Repository Shoko_Video_User { get; } = new();

    public static AnimeEpisodeRepository Shoko_Episode { get; } = new();

    public static ShokoEpisode_UserRepository Shoko_Episode_User { get; } = new();

    public static AnimeSeriesRepository Shoko_Series { get; } = new();

    public static ShokoSeries_UserRepository Shoko_Series_User { get; } = new();

    public static ShokoGroup_Repository Shoko_Group { get; } = new();

    public static ShokoGroup_UserRepository Shoko_Group_User { get; } = new();

    public static AniDB_VoteRepository AniDB_Vote { get; } = new();

    public static TvDB_EpisodeRepository TvDB_Episode { get; } = new();

    public static TvDB_SeriesRepository TvDB_Show { get; } = new();

    public static CR_AniDB_Trakt_Repository CR_AniDB_Trakt { get; } = new();

    public static CrossRef_AniDB_TvDBRepository CR_AniDB_TvDB { get; } = new();

    public static CrossRef_AniDB_TvDB_EpisodeRepository CR_AniDB_TvDB_Episode { get; } = new();

    public static CrossRef_AniDB_TvDB_Episode_OverrideRepository CR_AniDB_TvDB_Episode_Override { get; } = new();

    public static AnimeCharacterRepository Shoko_Character { get; } = new();

    public static AnimeStaffRepository Shoko_Staff { get; } = new();

    public static CrossRef_Anime_StaffRepository CR_ShokoSeries_ShokoStaff { get; } = new();

    public static GroupFilterRepository Shoko_Group_Filter { get; } = new();

    #endregion

    #region Depreacated
    // We need to delete them at some point.

    [Obsolete("To-be-removed when desktop/v1 is removed.")]
    public static PlaylistRepository Playlist { get; } = new();

    [Obsolete("No longer used.")]
    public static MovieDB_FanartRepository TMDB_Fanart { get; } = new();

    [Obsolete("No longer used.")]
    public static MovieDB_PosterRepository TMDB_Movie_Poster { get; } = new();

    [Obsolete("No longer used.")]
    public static TvDB_ImagePosterRepository TvDB_Poster { get; } = new();

    [Obsolete("No longer used.")]
    public static TvDB_ImageFanartRepository TvDB_Fanart { get; } = new();

    [Obsolete("No longer used.")]
    public static TvDB_ImageWideBannerRepository TvDB_Banner { get; } = new();

    #endregion


    public static void PostInit()
    {
        // Update Contracts if necessary
        try
        {
            Logger.Info("Starting Server: RepoFactory.PostInit()");
            CachedRepositories.ForEach(repo =>
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, repo.GetType().Name.Replace("Repository", ""), " DbRegen");
                repo.RegenerateDb();
            });
            CachedRepositories.ForEach(repo => repo.PostProcess());
        }
        catch (Exception e)
        {
            Logger.Error($"There was an error starting the Database Factory - Regenerating: {e}");
            throw;
        }

        CleanUpMemory();
    }

    public static async Task Init()
    {
        try
        {
            foreach (var repo in CachedRepositories)
            {
                repo.Populate();
            }
        }
        catch (Exception exception)
        {
            Logger.Error($"There was an error starting the Database Factory - Caching: {exception}");
            throw;
        }
    }

    public static void CleanUpMemory()
    {
        AniDB_Anime.GetAll().ForEach(a => a.CollectContractMemory());
        Shoko_Video.GetAll().ForEach(a => a.CollectContractMemory());
        Shoko_Series.GetAll().ForEach(a => a.CollectContractMemory());
        Shoko_Series_User.GetAll().ForEach(a => a.CollectContractMemory());
        Shoko_Group.GetAll().ForEach(a => a.CollectContractMemory());
        Shoko_Group_User.GetAll().ForEach(a => a.CollectContractMemory());

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
    }
}
