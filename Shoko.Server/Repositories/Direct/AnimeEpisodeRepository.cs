using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Repositories.Cached;

public class AnimeEpisodeRepository : BaseCachedRepository<Shoko_Episode, int>
{
    private PocoIndex<int, Shoko_Episode, int> Series;
    private PocoIndex<int, Shoko_Episode, int> EpisodeIDs;

    public AnimeEpisodeRepository()
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.Shoko_Episode_User.Delete(
                RepoFactory.Shoko_Episode_User.GetByEpisodeID(cr.Id));
        };
    }

    protected override int SelectKey(Shoko_Episode entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
        Series = Cache.CreateIndex(a => a.SeriesId);
        EpisodeIDs = Cache.CreateIndex(a => a.AnidbEpisodeId);
    }

    public override void RegenerateDb()
    {
    }

    public List<Shoko_Episode> GetBySeriesID(int seriesid)
    {
        return ReadLock(() => Series.GetMultiple(seriesid));
    }


    public Shoko_Episode GetByAnidbEpisodeId(int epid)
    {
        return ReadLock(() => EpisodeIDs.GetOne(epid));
    }


    /// <summary>
    /// Get the AnimeEpisode 
    /// </summary>
    /// <param name="name">The filename of the anime to search for.</param>
    /// <returns>the AnimeEpisode given the file information</returns>
    public Shoko_Episode GetByFilename(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var eps = RepoFactory.Shoko_Video_Location.GetAll()
            .Where(v => name.Equals(v?.RelativePath?.Split(Path.DirectorySeparatorChar).LastOrDefault(), StringComparison.InvariantCultureIgnoreCase))
            .Select(a => RepoFactory.Shoko_Video.GetByID(a.VideoId))
            .Where(a => a != null)
            .SelectMany(a => GetByHash(a.ED2K))
            .ToArray();
        var ep = eps.FirstOrDefault(a => a.AniDB.Type == (int)EpisodeType.Episode);
        return ep ?? eps.FirstOrDefault();
    }


    /// <summary>
    /// Get all the AnimeEpisode records associate with an AniDB_File record
    /// AnimeEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
    /// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
    /// CrossRef_File_Episode.Hash -> VideoLocal.Hash
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public List<Shoko_Episode> GetByHash(string hash)
    {
        return RepoFactory.CR_Video_Episode.GetByED2K(hash)
            .Select(a => GetByAnidbEpisodeId(a.AnidbEpisodeId))
            .Where(a => a != null)
            .ToList();
    }

    public List<Shoko_Episode> GetEpisodesWithMultipleFiles(bool ignoreVariations)
    {
        IEnumerable<int> ids;
        lock (GlobalDBLock)
        {
            const string ignoreVariationsQuery =
                @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
            const string countVariationsQuery =
                @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";

            using var session = DatabaseFactory.SessionFactory.OpenSession();
            ids = ignoreVariations
                ? session.CreateSQLQuery(ignoreVariationsQuery).List<object>().Select(Convert.ToInt32)
                : session.CreateSQLQuery(countVariationsQuery).List<object>().Select(Convert.ToInt32);
        }

        return ids.Select(GetByAnidbEpisodeId).Where(a => a != null).ToList();
    }

    public List<Shoko_Episode> GetUnwatchedEpisodes(int seriesid, int userid)
    {
        var eps =
            RepoFactory.Shoko_Episode_User.GetByUserIDAndSeriesID(userid, seriesid)
                .Where(a => a.LastWatchedAt.HasValue)
                .Select(a => a.EpisodeId)
                .ToList();
        return GetBySeriesID(seriesid).Where(a => !eps.Contains(a.Id)).ToList();
    }

    public List<Shoko_Episode> GetAllWatchedEpisodes(int userid, DateTime? after_date)
    {
        return RepoFactory.Shoko_Episode_User.GetByUserID(userid)
            .Where(a => a.LastWatchedAt.HasValue && after_date.HasValue ? a.LastWatchedAt.Value > after_date.Value : true)
            .OrderBy(a => a.LastWatchedAt)
            .Select(userRecord => GetByID(userRecord.EpisodeId))
            .ToList();
    }

    public List<Shoko_Episode> GetEpisodesWithNoFiles(bool includeSpecials)
    {
        var all = GetAll().Where(a =>
            {
                var aniep = a.AniDB;
                if (aniep?.GetFutureDated() != false)
                {
                    return false;
                }

                if (aniep.Type != (int)EpisodeType.Episode &&
                    aniep.Type != (int)EpisodeType.Special)
                {
                    return false;
                }

                if (!includeSpecials &&
                    aniep.Type == (int)EpisodeType.Special)
                {
                    return false;
                }

                return a.Videos.Count == 0;
            })
            .ToList();
        all.Sort((a1, a2) =>
        {
            var name1 = a1.GetAnimeSeries()?.GetSeriesName();
            var name2 = a2.GetAnimeSeries()?.GetSeriesName();

            if (!string.IsNullOrEmpty(name1) && !string.IsNullOrEmpty(name2))
            {
                return string.Compare(name1, name2, StringComparison.Ordinal);
            }

            if (string.IsNullOrEmpty(name1))
            {
                return 1;
            }

            if (string.IsNullOrEmpty(name2))
            {
                return -1;
            }

            return a1.AnimeSeriesID.CompareTo(a2.AnimeSeriesID);
        });

        return all;
    }
}
