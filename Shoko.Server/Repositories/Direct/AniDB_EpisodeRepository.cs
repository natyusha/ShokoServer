using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories;

public class AniDB_EpisodeRepository : BaseCachedRepository<AniDB_Episode, int>
{
    private PocoIndex<int, AniDB_Episode, int>? EpisodesIds;
    private PocoIndex<int, AniDB_Episode, int>? Animes;

    public override void PopulateIndexes()
    {
        EpisodesIds = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.EpisodeId);
        Animes = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.AnimeId);
    }

    protected override int SelectKey(AniDB_Episode entity)
    {
        return entity.AniDB_EpisodeID;
    }

    public override void RegenerateDb()
    {
    }

    public AniDB_Episode? GetByAnidbEpisodeId(int id)
    {
        return ReadLock(() => EpisodesIds!.GetOne(id));
    }

    public List<AniDB_Episode> GetByAnidbAnimeId(int id)
    {
        return ReadLock(() => Animes!.GetMultiple(id));
    }

    public List<AniDB_Episode> GetForDate(DateTime startDate, DateTime endDate)
    {
        return ReadLock(() => Cache.Values.Where(a =>
        {
            var date = a.AirDate;
            return date.HasValue && date.Value >= startDate && date.Value <= endDate;
        }).ToList());
    }

    public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
    {
        return GetByAnidbAnimeId(animeid)
            .Where(a => a.Number == epnumber && a.Type == EpisodeType.Episode)
            .ToList();
    }

    public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, EpisodeType epType, int epnumber)
    {
        return GetByAnidbAnimeId(animeid)
            .Where(a => a.Number == epnumber && a.Type == epType)
            .ToList();
    }
}
