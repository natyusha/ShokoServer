using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Models.CrossReferences;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TvDBRepository : BaseCachedRepository<CR_AniDB_TvDB, int>
{
    private PocoIndex<int, CR_AniDB_TvDB, int> TvDBIDs;
    private PocoIndex<int, CR_AniDB_TvDB, int> AnimeIDs;

    public override void PopulateIndexes()
    {
        TvDBIDs = new PocoIndex<int, CR_AniDB_TvDB, int>(Cache, a => a.TvdbShowId);
        AnimeIDs = new PocoIndex<int, CR_AniDB_TvDB, int>(Cache, a => a.AnidbAnimeId);
    }

    public List<CR_AniDB_TvDB> GetByAnimeID(int id)
    {
        return ReadLock(() => AnimeIDs.GetMultiple(id));
    }

    public List<CR_AniDB_TvDB> GetByTvDBId(int id)
    {
        return ReadLock(() => TvDBIDs.GetMultiple(id));
    }

    public ILookup<int, CR_AniDB_TvDB> GetByAnidbIds(IReadOnlyCollection<int> animeIds)
    {
        if (animeIds == null)
        {
            throw new ArgumentNullException(nameof(animeIds));
        }

        if (animeIds.Count == 0)
        {
            return EmptyLookup<int, CR_AniDB_TvDB>.Instance;
        }

        return ReadLock(() => animeIds.SelectMany(id => AnimeIDs.GetMultiple(id))
            .ToLookup(xref => xref.AnidbAnimeId));
    }

    public CR_AniDB_TvDB GetByAnidbAndTvdbIds(int anidbAnimeID, int tvdbID)
    {
        return ReadLock(() => TvDBIDs.GetMultiple(tvdbID).FirstOrDefault(xref => xref.AnidbAnimeId == anidbAnimeID));
    }

    public List<ShokoSeries> GetSeriesWithoutLinks()
    {
        return RepoFactory.Shoko_Series.GetAll().Where(a =>
        {
            if (a.IsTvDBAutoMatchingDisabled)
            {
                return false;
            }

            var anime = a.GetAnime();
            if (anime == null)
            {
                return false;
            }

            if (anime.Restricted > 0)
            {
                return false;
            }

            if (anime.AnimeType == AnimeType.Movie)
            {
                return false;
            }

            return !GetByAnimeID(a.AniDB_ID).Any();
        }).ToList();
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(CR_AniDB_TvDB entity)
    {
        return entity.Id;
    }

    public List<CL_CrossRef_AniDB_TvDB> GetV2LinksFromAnime(int animeID)
    {
        var overrides = RepoFactory.CR_AniDB_TvDB_Episode_Override.GetByAnimeID(animeID);
        var normals = RepoFactory.CR_AniDB_TvDB_Episode.GetByAnimeID(animeID);
        var ls = new List<(int anidb_episode, int tvdb_episode)>();
        foreach (var epo in normals)
        {
            var ov = overrides.FirstOrDefault(a => a.AniDBEpisodeID == epo.AnidbEpisodeId);
            if (ov != null)
            {
                ls.Add((ov.AniDBEpisodeID, ov.TvDBEpisodeID));
                overrides.Remove(ov);
            }
            else
            {
                ls.Add((epo.AnidbEpisodeId, epo.TvdbEpisodeId));
            }
        }

        ls.AddRange(overrides.Select(ov => (ov.AniDBEpisodeID, ov.TvDBEpisodeID)));
        var eplinks = ls.ToLookup(a => RepoFactory.AniDB_Episode.GetByAnidbEpisodeId(a.anidb_episode),
                b => RepoFactory.TvDB_Episode.GetByTvDBID(b.tvdb_episode))
            .Select(a => (AniDB: a.Key, TvDB: a.FirstOrDefault())).Where(a => a.AniDB != null && a.TvDB != null)
            .OrderBy(a => a.AniDB.Type).ThenBy(a => a.AniDB.Number).ToList();

        var output = new List<(int EpisodeType, int EpisodeNumber, int TvDBSeries, int TvDBSeason, int TvDBNumber)>();
        for (var i = 0; i < eplinks.Count; i++)
        {
            // Cases:
            // - first ep
            // - new type/season
            // - the next episode is not a simple increment
            var b = eplinks[i];
            if (i == 0)
            {
                if (b.AniDB == null || b.TvDB == null)
                {
                    return new List<CL_CrossRef_AniDB_TvDB>();
                }

                output.Add((b.AniDB.Type, b.AniDB.Number, b.TvDB.SeriesID, b.TvDB.SeasonNumber,
                    b.TvDB.EpisodeNumber));
                continue;
            }

            var a = eplinks[i - 1];
            if (a.AniDB.Type != b.AniDB.Type || b.TvDB.SeasonNumber != a.TvDB.SeasonNumber)
            {
                output.Add((b.AniDB.Type, b.AniDB.Number, b.TvDB.SeriesID, b.TvDB.SeasonNumber,
                    b.TvDB.EpisodeNumber));
                continue;
            }

            if (b.AniDB.Number - a.AniDB.Number != 1 || b.TvDB.EpisodeNumber - a.TvDB.EpisodeNumber != 1)
            {
                output.Add((b.AniDB.Type, b.AniDB.Number, b.TvDB.SeriesID, b.TvDB.SeasonNumber,
                    b.TvDB.EpisodeNumber));
            }
        }

        return output.Select(a => new CL_CrossRef_AniDB_TvDB
        {
            AnimeID = animeID,
            AniDBStartEpisodeType = a.EpisodeType,
            AniDBStartEpisodeNumber = a.EpisodeNumber,
            TvDBID = a.TvDBSeries,
            TvDBSeasonNumber = a.TvDBSeason,
            TvDBStartEpisodeNumber = a.TvDBNumber,
            TvDBTitle = RepoFactory.TvDB_Show.GetByShowId(a.TvDBSeries)?.MainTitle
        }).ToList();
    }
}
