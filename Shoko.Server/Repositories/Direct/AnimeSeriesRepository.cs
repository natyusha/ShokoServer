using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached;

public class AnimeSeriesRepository : BaseCachedRepository<ShokoSeries, int>
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    private PocoIndex<int, ShokoSeries, int> AniDBIds;
    private PocoIndex<int, ShokoSeries, int> Groups;

    private ChangeTracker<int> Changes = new();

    public AnimeSeriesRepository()
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.Shoko_Series_User.Delete(RepoFactory.Shoko_Series_User.GetBySeriesId(cr.Id));
            Changes.Remove(cr.Id);
        };
        EndDeleteCallback = cr =>
        {
            cr.DeleteFromFilters();
            if (cr.ParentGroupId <= 0)
            {
                return;
            }

            logger.Trace("Updating group stats by group from AnimeSeriesRepository.Delete: {0}",
                cr.ParentGroupId);
            var oldGroup = RepoFactory.Shoko_Group.GetByID(cr.ParentGroupId);
            if (oldGroup != null)
            {
                RepoFactory.Shoko_Group.Save(oldGroup, true, true);
            }
        };
    }

    protected override int SelectKey(ShokoSeries entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
        Changes.AddOrUpdateRange(Cache.Keys);
        AniDBIds = Cache.CreateIndex(a => a.AniDB_ID);
        Groups = Cache.CreateIndex(a => a.ParentGroupId);
    }

    public override void RegenerateDb()
    {
        try
        {
            var sers =
                Cache.Values.Where(
                        a => a.ContractVersion < ShokoSeries.CONTRACT_VERSION ||
                             a.Contract?.AniDBAnime?.AniDBAnime == null)
                    .ToList();
            var max = sers.Count;
            ServerState.Instance.ServerStartingStatus = string.Format(
                Resources.Database_Validating, typeof(ShokoSeries).Name, " DbRegen");
            if (max <= 0)
            {
                return;
            }

            for (var i = 0; i < sers.Count; i++)
            {
                var s = sers[i];
                try
                {
                    Save(s, false, false, true);
                }
                catch
                {
                }

                if (i % 10 == 0)
                {
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, typeof(ShokoSeries).Name,
                        " DbRegen - " + i + "/" + max
                    );
                }
            }

            ServerState.Instance.ServerStartingStatus = string.Format(
                Resources.Database_Validating, typeof(ShokoSeries).Name,
                " DbRegen - " + max + "/" + max);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }

    public override void Save(ShokoSeries obj)
    {
        Save(obj, false);
    }

    public void Save(ShokoSeries obj, bool onlyupdatestats)
    {
        Save(obj, true, onlyupdatestats);
    }

    public void Save(ShokoSeries obj, bool updateGroups, bool onlyupdatestats, bool skipgroupfilters = false,
        bool alsoupdateepisodes = false)
    {
        var animeID = obj.GetAnime()?.MainTitle ?? obj.AniDB_ID.ToString();
        logger.Trace($"Saving Series {animeID}");
        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        var newSeries = false;
        ShokoGroup oldGroup = null;
        // Updated Now
        obj.DateTimeUpdated = DateTime.Now;
        var isMigrating = false;
        if (obj.Id == 0)
        {
            newSeries = true; // a new series
        }
        else
        {
            // get the old version from the DB
            ShokoSeries oldSeries;
            logger.Trace($"Saving Series {animeID} | Waiting for Database Lock");
            lock (GlobalDBLock)
            {
                sw.Stop();
                logger.Trace($"Saving Series {animeID} | Got Database Lock in {sw.Elapsed.TotalSeconds:0.00###}s");
                sw.Restart();
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                oldSeries = session.Get<ShokoSeries>(obj.Id);
                sw.Stop();
                logger.Trace($"Saving Series {animeID} | Got Series from Database in {sw.Elapsed.TotalSeconds:0.00###}s");
                sw.Restart();
            }

            if (oldSeries != null)
            {
                // means we are moving series to a different group
                if (oldSeries.ParentGroupId != obj.ParentGroupId)
                {
                    logger.Trace($"Saving Series {animeID} | Group ID is different. Moving to new group");
                    oldGroup = RepoFactory.Shoko_Group.GetByID(oldSeries.ParentGroupId);
                    var newGroup = RepoFactory.Shoko_Group.GetByID(obj.ParentGroupId);
                    if (newGroup is { GroupName: "AAA Migrating Groups AAA" })
                    {
                        isMigrating = true;
                    }

                    newSeries = true;
                }
            }
            else
            {
                // should not happen, but if it does, recover
                newSeries = true;
                logger.Trace(
                    $"Saving Series {animeID} | Unable to get series from database, attempting to make new record");
            }
        }

        if (newSeries && !isMigrating)
        {
            sw.Stop();
            logger.Trace($"Saving Series {animeID} | New Series added. Need to save first to get an ID");
            sw.Restart();
            obj.Contract = null;
            base.Save(obj);
            sw.Stop();
            logger.Trace($"Saving Series {animeID} | Saved new series in {sw.Elapsed.TotalSeconds:0.00###}s");
            sw.Restart();
        }

        var seasons = obj.GetAnime()?.Contract?.Stat_AllSeasons;
        if (seasons == null || seasons.Count == 0) RegenerateSeasons(obj, sw, animeID);

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updating Series Contract");
        sw.Restart();
        var types = obj.UpdateContract(onlyupdatestats);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Series Contract in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
        logger.Trace($"Saving Series {animeID} | Saving Series to Database");
        base.Save(obj);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Saved Series to Database in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        if (updateGroups && !isMigrating) UpdateGroups(obj, animeID, sw, oldGroup);

        if (!skipgroupfilters && !isMigrating) UpdateGroupFilters(obj, sw, animeID, types);

        Changes.AddOrUpdate(obj.Id);

        if (alsoupdateepisodes) UpdateEpisodes(obj, sw, animeID);

        sw.Stop();
        totalSw.Stop();
        logger.Trace($"Saving Series {animeID} | Finished Saving in {totalSw.Elapsed.TotalSeconds:0.00###}s");
    }

    private static void RegenerateSeasons(ShokoSeries obj, Stopwatch sw, string animeID)
    {
        sw.Stop();
        logger.Trace(
            $"Saving Series {animeID} | AniDB_Anime Contract is invalid or Seasons not generated. Regenerating");
        sw.Restart();
        var anime = obj.GetAnime();
        if (anime != null)
        {
            RepoFactory.AniDB_Anime.Save(anime, true);
        }

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Regenerated AniDB_Anime Contract in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
    }

    private static void UpdateEpisodes(ShokoSeries obj, Stopwatch sw, string animeID)
    {
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updating Episodes");
        sw.Restart();
        var eps = RepoFactory.Shoko_Episode.GetBySeriesID(obj.Id);
        RepoFactory.Shoko_Episode.Save(eps);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Episodes in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
    }

    private static void UpdateGroupFilters(ShokoSeries obj, Stopwatch sw, string animeID, HashSet<GroupFilterConditionType> types)
    {
        SortedSet<string> seasons;
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updating Group Filters");
        sw.Restart();
        var endyear = obj.Contract?.AniDBAnime?.AniDBAnime?.EndYear ?? 0;
        if (endyear == 0)
        {
            endyear = DateTime.Today.Year;
        }

        var startyear = obj.Contract?.AniDBAnime?.AniDBAnime?.BeginYear ?? 0;
        if (endyear < startyear)
        {
            endyear = startyear;
        }

        HashSet<int> allyears = null;
        if (startyear != 0)
        {
            allyears = startyear == endyear
                ? new HashSet<int> { startyear }
                : new HashSet<int>(Enumerable.Range(startyear, endyear - startyear + 1));
        }

        // Reinit this in case it was updated in the contract
        seasons = obj.Contract?.AniDBAnime?.Stat_AllSeasons;
        //This call will create extra years or tags if the Group have a new year or tag
        logger.Trace(
            $"Saving Series {animeID} | Updating Group Filters for Years ({string.Join(",", (IEnumerable<int>)allyears?.OrderBy(a => a) ?? Array.Empty<int>())}) and Seasons ({string.Join(",", (IEnumerable<string>)seasons ?? Array.Empty<string>())})");
        RepoFactory.Shoko_Group_Filter.CreateOrVerifyDirectoryFilters(false,
            obj.Contract?.AniDBAnime?.AniDBAnime?.GetAllTags(), allyears, seasons);

        // Update other existing filters
        obj.UpdateGroupFilters(types);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Group Filters in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
    }

    private static void UpdateGroups(ShokoSeries obj, string animeID, Stopwatch sw, ShokoGroup oldGroup)
    {
        logger.Trace($"Saving Series {animeID} | Also Updating Group {obj.ParentGroupId}");
        var grp = RepoFactory.Shoko_Group.GetByID(obj.ParentGroupId);
        if (grp != null)
        {
            RepoFactory.Shoko_Group.Save(grp, true, true);
        }
        else
            logger.Trace($"Saving Series {animeID} | Group {obj.ParentGroupId} was not found. Not Updating");

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Group {obj.ParentGroupId} in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        // Last ditch to make sure we aren't just updating the same group twice (shouldn't be)
        if (oldGroup != null && grp?.Id != oldGroup.AnimeGroupID)
        {
            logger.Trace($"Saving Series {animeID} | Also Updating previous group {oldGroup.AnimeGroupID}");
            RepoFactory.Shoko_Group.Save(oldGroup, true, true);
            sw.Stop();
            logger.Trace(
                $"Saving Series {animeID} | Updated old group {oldGroup.AnimeGroupID} in {sw.Elapsed.TotalSeconds:0.00###}s");
            sw.Restart();
        }
    }

    public void UpdateBatch(ISessionWrapper session, IReadOnlyCollection<ShokoSeries> seriesBatch)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (seriesBatch == null)
        {
            throw new ArgumentNullException(nameof(seriesBatch));
        }

        if (seriesBatch.Count == 0)
        {
            return;
        }

        foreach (var series in seriesBatch)
        {
            lock (GlobalDBLock) session.Update(series);
            UpdateCache(series);
            Changes.AddOrUpdate(series.Id);
        }
    }

    public ShokoSeries GetByAnidbAnimeId(int id)
    {
        return ReadLock(() => AniDBIds.GetOne(id));
    }

    public List<ShokoSeries> GetByGroupID(int groupid)
    {
        return ReadLock(() => Groups.GetMultiple(groupid));
    }

    public List<ShokoSeries> GetWithMissingEpisodes()
    {
        return ReadLock(() => Cache.Values.Where(a => a.MissingEpisodeCountGroups > 0)
            .OrderByDescending(a => a.EpisodeAddedDate)
            .ToList());
    }

    public List<ShokoSeries> GetMostRecentlyAdded(int maxResults, int userID)
    {
        var user = RepoFactory.Shoko_User.GetByID(userID);
        return ReadLock(() => user == null
            ? Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList()
            : Cache.Values.Where(a => user.AllowedSeries(a)).OrderByDescending(a => a.DateTimeCreated).Take(maxResults)
                .ToList());
    }
}
