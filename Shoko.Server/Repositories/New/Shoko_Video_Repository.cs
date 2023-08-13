using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.LZ4;
using Shoko.Server.Models;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Models.Internal;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.MediaInfoLib;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class Shoko_Video_Repository : BaseCachedRepository<Shoko_Video, int>
{
    private PocoIndex<int, Shoko_Video, string>? ED2KIndex;

    private PocoIndex<int, Shoko_Video, string?>? SHA1Index;

    private PocoIndex<int, Shoko_Video, string?>? MD5Index;

    private PocoIndex<int, Shoko_Video, string?>? CRC32Index;

    private PocoIndex<int, Shoko_Video, bool>? IsIgnoredIndex;

    public Shoko_Video_Repository()
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
        {
            RepoFactory.Shoko_Video_Location.DeleteWithOpenTransaction(ses, obj.Locations.ToList());
            RepoFactory.Shoko_Video_User.DeleteWithOpenTransaction(
                ses,
                RepoFactory.Shoko_Video_User.GetByVideoId(obj.Id)
            );
        };
    }

    protected override int SelectKey(Shoko_Video entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
        //Fix null hashes
#pragma warning disable 0618
        foreach (var l in Cache.Values)
        {
            if (l.ED2K == null || l.FileName == null)
            {
                l.MediaVersion = 0;

                if (l.ED2K == null)
                {
                    l.ED2K = string.Empty;
                }

                if (l.FileName == null)
                {
                    l.FileName = string.Empty;
                }
            }
        }
#pragma warning restore 0618

        ED2KIndex = new(Cache, a => a.ED2K);
        SHA1Index = new(Cache, a => a.SHA1);
        MD5Index = new(Cache, a => a.MD5);
        CRC32Index = new(Cache, a => a.CRC32);
        IsIgnoredIndex = new(Cache, a => a.IsIgnored);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(Shoko_Video), " Checking Media Info"
        );
        var count = 0;
        int max;

        var list = Cache.Values.Where(a => a is { MediaVersion: 4, MediaBlob: { Length: > 0 } }).ToList();
        max = list.Count;

        foreach (var batch in list.Batch(50))
        {
            using var session2 = DatabaseFactory.SessionFactory.OpenSession();
            using var transaction = session2.BeginTransaction();
            foreach (var a in batch)
            {
                var media = CompressionHelper.DeserializeObject<MediaContainer>(a.MediaBlob, a.MediaSize,
                    new JsonConverter[] { new StreamJsonConverter() });
                a.Media = media;
                RepoFactory.Shoko_Video.SaveWithOpenTransaction(session2, a);
                count++;
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(Shoko_Video),
                    " Converting MediaInfo to MessagePack - " + count + "/" + max
                );
            }

            transaction.Commit();
        }

        count = 0;
        try
        {
            list = Cache.Values.Where(a =>
                    (a.MediaVersion < Shoko_Video.MEDIA_VERSION &&
                     !(Shoko_Video.MEDIA_VERSION == 5 && a.MediaVersion == 4)) || a.MediaBlob == null)
                .ToList();
            max = list.Count;

            var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
            list.ForEach(
                a =>
                {
                    var cmd = commandFactory.Create<CommandRequest_ReadMediaInfo>(c => c.VideoLocalID = a.Id);
                    cmd.Save();
                    count++;
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, nameof(Shoko_Video),
                        " Queuing Media Info Commands - " + count + "/" + max
                    );
                }
            );
        }
        catch
        {
            // ignore
        }

        var locals = Cache.Values
            .Where(a => !string.IsNullOrWhiteSpace(a.ED2K))
            .GroupBy(a => a.ED2K)
            .ToDictionary(g => g.Key, g => g.ToList());
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(Shoko_Video),
            " Cleaning Empty Records"
        );
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        using (var transaction = session.BeginTransaction())
        {
            list = Cache.Values.Where(a => a.IsEmpty()).ToList();
            count = 0;
            max = list.Count;
            foreach (var remove in list)
            {
                RepoFactory.Shoko_Video.DeleteWithOpenTransaction(session, remove);
                count++;
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(Shoko_Video),
                    " Cleaning Empty Records - " + count + "/" + max
                );
            }

            transaction.Commit();
        }

        var toRemove = new List<Shoko_Video>();
        var comparer = new ShokoVideoComparer();

        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(Shoko_Video),
            " Checking for Duplicate Records"
        );

        foreach (var hash in locals.Keys)
        {
            var values = locals[hash];
            values.Sort(comparer);
            var to = values.First();
            var froms = values.Except(to).ToList();
            foreach (var from in froms)
            {
                var places = from.Locations;
                if (places == null || places.Count == 0)
                {
                    continue;
                }

                using var transaction = session.BeginTransaction();
                foreach (var place in places)
                {
                    place.VideoId = to.Id;
                    RepoFactory.Shoko_Video_Location.SaveWithOpenTransaction(session, place);
                }

                transaction.Commit();
            }

            toRemove.AddRange(froms);
        }

        count = 0;
        max = toRemove.Count;
        foreach (var batch in toRemove.Batch(50))
        {
            using var transaction = session.BeginTransaction();
            foreach (var remove in batch)
            {
                count++;
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(Shoko_Video),
                    " Cleaning Duplicate Records - " + count + "/" + max
                );
                DeleteWithOpenTransaction(session, remove);
            }

            transaction.Commit();
        }
    }

    private void UpdateMediaContracts(Shoko_Video obj)
    {
        if (obj.Media != null && obj.MediaVersion >= Shoko_Video.MEDIA_VERSION)
        {
            return;
        }

        obj.RefreshMediaInfo();
    }

    public override void Delete(Shoko_Video obj)
    {
        var list = obj.GetEpisodes();
        base.Delete(obj);
        list.Where(a => a != null).ForEach(a => RepoFactory.Shoko_Episode.Save(a));
    }

    public override void Save(Shoko_Video obj)
    {
        Save(obj, true);
    }

    public void Save(Shoko_Video obj, bool updateEpisodes)
    {
        if (obj.Id == 0)
        {
            obj.Media = null;
            base.Save(obj);
        }

        UpdateMediaContracts(obj);
        base.Save(obj);

        if (updateEpisodes)
        {
            RepoFactory.Shoko_Episode.Save(obj.GetEpisodes());
        }
    }

    public IReadOnlyList<Shoko_Video> GetByImportFolderId(int importFolderId)
    {
        return RepoFactory.Shoko_Video_Location.GetByImportFolderId(importFolderId)
            .Select(a => GetByID(a.VideoId))
            .Where(a => a != null)
            .Distinct()
            .ToList();
    }

    public Shoko_Video? GetByED2K(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return null;
        return ReadLock(() => ED2KIndex!.GetOne(hash));
    }

    public Shoko_Video? GetByMD5(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return null;
        return ReadLock(() => MD5Index!.GetOne(hash));
    }

    public Shoko_Video? GetBySHA1(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return null;
        return ReadLock(() => SHA1Index!.GetOne(hash));
    }

    public Shoko_Video? GetByCRC32(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return null;
        return ReadLock(() => CRC32Index!.GetOne(hash));
    }

    public IReadOnlyList<Shoko_Video> GetByRelativePathFuzzy(string fileName)
    {
        return ReadLock(
            () => Cache.Values
                .Where(
                    video => video.Locations.Any(
                        location => location.RelativePath.FuzzyMatch(fileName)
                    )
                )
                .ToList()
        );
    }

    public IReadOnlyList<Shoko_Video> GetMostRecentlyAdded(int maxResults, int jmmuserID)
    {
        var user = RepoFactory.Shoko_User.GetByID(jmmuserID);
        if (user == null)
        {
            return ReadLock(() =>
                maxResults == -1
                    ? Cache.Values.OrderByDescending(a => a.CreatedAt).ToList()
                    : Cache.Values.OrderByDescending(a => a.CreatedAt).Take(maxResults).ToList());
        }

        if (maxResults == -1)
        {
            return ReadLock(
                () => Cache.Values
                    .Where(
                        a => a.GetEpisodes().Select(b => b.Series).Where(b => b != null)
                            .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries)
                    ).OrderByDescending(a => a.CreatedAt)
                    .ToList()
            );
        }

        return ReadLock(
            () => Cache.Values
                .Where(a => a.GetEpisodes().Select(b => b.Series).Where(b => b != null)
                    .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries)).OrderByDescending(a => a.CreatedAt)
                .Take(maxResults).ToList()
        );
    }

    public IReadOnlyList<Shoko_Video> GetMostRecentlyAdded(int take, int skip, int jmmuserID)
    {
        if (skip < 0)
        {
            skip = 0;
        }

        if (take == 0)
        {
            return new List<Shoko_Video>();
        }

        var user = jmmuserID == -1 ? null : RepoFactory.Shoko_User.GetByID(jmmuserID);
        if (user == null)
        {
            return ReadLock(() =>
                take == -1
                    ? Cache.Values.OrderByDescending(a => a.CreatedAt).Skip(skip).ToList()
                    : Cache.Values.OrderByDescending(a => a.CreatedAt).Skip(skip).Take(take).ToList());
        }

        return ReadLock(
            () => take == -1
                ? Cache.Values
                    .Where(a => a.GetEpisodes().Select(b => b.Series).Where(b => b != null)
                        .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries))
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip(skip)
                    .ToList()
                : Cache.Values
                    .Where(a => a.GetEpisodes().Select(b => b.Series).Where(b => b != null)
                        .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries))
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToList()
        );
    }

    public IReadOnlyList<Shoko_Video> GetRandomFiles(int maxResults)
    {
        var values = ReadLock(Cache.Values.ToList).Where(a => a.GetCrossReferences(true).Any()).ToList();

        using var en = new UniqueRandoms(0, values.Count - 1).GetEnumerator();
        var vids = new List<Shoko_Video>();
        if (maxResults > values.Count)
        {
            maxResults = values.Count;
        }

        for (var x = 0; x < maxResults; x++)
        {
            en.MoveNext();
            vids.Add(values.ElementAt(en.Current));
        }

        return vids;
    }

    public class UniqueRandoms : IEnumerable<int>
    {
        private readonly Random _rand = new();
        private readonly List<int> _candidates;

        public UniqueRandoms(int minInclusive, int maxInclusive)
        {
            _candidates =
                Enumerable.Range(minInclusive, maxInclusive - minInclusive + 1).ToList();
        }

        public IEnumerator<int> GetEnumerator()
        {
            while (_candidates.Count > 0)
            {
                var index = _rand.Next(_candidates.Count);
                yield return _candidates[index];
                _candidates.RemoveAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    public IReadOnlyList<Shoko_Video> GetMostRecentlyAddedForAnime(int anidbAnimeId, int maxResults = 1)
    {
        return RepoFactory.CR_Video_Episode.GetByAnidbAnimeId(anidbAnimeId)
            .Select(a => GetByED2K(a.ED2K)!)
            .Where(a => a != null)
            .OrderByDescending(a => a.CreatedAt)
            .Take(maxResults)
            .ToList();
    }

    public IReadOnlyList<Shoko_Video> GetByInternalVersion(int iver)
    {
        return RepoFactory.AniDB_File.GetByInternalVersion(iver)
            .Select(a => GetByED2K(a.ED2K)!)
            .Where(a => a != null)
            .ToList();
    }


    /// <summary>
    /// returns all the ShokoVideo records associate with an AnimeEpisode Record
    /// </summary>
    /// <param name="episodeID">AniDB Episode ID</param>
    /// <param name="xrefSource">Include to select only files from the selected
    /// cross-reference source.</param>
    /// <returns></returns>
    /// 
    public IReadOnlyList<Shoko_Video> GetByAnidbEpisodeId(int episodeID, CrossRefSource? xrefSource = null)
    {
        if (xrefSource.HasValue)
            return RepoFactory.CR_Video_Episode.GetByAniDBEpisodeId(episodeID)
                .Where(xref => xref.CrossReferenceSource == xrefSource.Value)
                .Select(xref => GetByED2K(xref.ED2K)!)
                .Where(video => video != null)
                .ToList();

        return RepoFactory.CR_Video_Episode.GetByAniDBEpisodeId(episodeID)
            .Select(xref => GetByED2K(xref.ED2K)!)
            .Where(video => video != null)
            .ToList();
    }

    /// <summary>
    /// returns all the ShokoVideo records associate with an AniDB_Anime Record
    /// </summary>
    /// <param name="animeID">AniDB Anime ID</param>
    /// <param name="xrefSource">Include to select only files from the selected
    /// cross-reference source.</param>
    /// <returns></returns>
    public IReadOnlyList<Shoko_Video> GetByAnidbAnimeId(int animeID, CrossRefSource? xrefSource = null)
    {
        if (xrefSource.HasValue)
            return RepoFactory.CR_Video_Episode.GetByAnidbAnimeId(animeID)
                .Where(xref => xref.CrossReferenceSource == xrefSource.Value)
                .Select(xref => GetByED2K(xref.ED2K)!)
                .Where(video => video != null)
                .ToList();

        return RepoFactory.CR_Video_Episode.GetByAnidbAnimeId(animeID)
            .Select(xref => GetByED2K(xref.ED2K)!)
            .Where(video => video != null)
            .ToList();
    }

    public IReadOnlyList<Shoko_Video> GetVideosWithoutHash()
    {
        return ReadLock(() => ED2KIndex!.GetMultiple(""));
    }

    public IReadOnlyList<Shoko_Video> GetVideosWithoutEpisode(bool includeBrokenXRefs = false)
    {
        return ReadLock(
            () => Cache.Values
                .Where( a =>
                {
                    if (a.IsIgnored)
                        return false;

                    var xrefs = RepoFactory.CR_Video_Episode.GetByED2K(a.ED2K);
                    if (!xrefs.Any())
                        return true;

                    if (includeBrokenXRefs)
                        return !xrefs.Any(IsImported);

                    return false;
                })
                .OrderByNatural(local =>
                {
                    var place = local?.GetPreferredLocation();
                    if (place == null) return null;
                    return place.AbsolutePath ?? place.RelativePath;
                })
                .ThenBy(local => local?.Id ?? int.MaxValue)
                .ToList()
        );
    }

    public IReadOnlyList<Shoko_Video> GetVideosWithMissingCrossReferenceData()
    {
        return ReadLock(
            () => Cache.Values
                .Where( a =>
                {
                    if (a.IsIgnored)
                        return false;

                    var xrefs = RepoFactory.CR_Video_Episode.GetByED2K(a.ED2K);
                    if (!xrefs.Any())
                        return false;

                    return !xrefs.All(IsImported);
                })
                .OrderByNatural(local =>
                {
                    var place = local?.GetPreferredLocation();
                    if (place == null) return null;
                    return place.AbsolutePath ?? place.RelativePath;
                })
                .ThenBy(local => local?.Id ?? int.MaxValue)
                .ToList()
        );
    }

    private static bool IsImported(CR_Video_Episode xref)
    {
        // If the shoko episode, anidb episode, shoko series, and anidb anime are not null,
        // then it's considered as "imported".
        return (xref.Episode?.AniDB != null && xref.Series?.GetAnime() != null);
    }

    public IReadOnlyList<Shoko_Video> GetVideosWithoutEpisodeUnsorted()
    {
        return ReadLock(() =>
            Cache.Values.Where(a => !a.IsIgnored && !a.GetCrossReferences(true).Any())
                .ToList());
    }

    public IReadOnlyList<Shoko_Video> GetManuallyLinkedVideos()
    {
        return
            RepoFactory.CR_Video_Episode.GetAll()
                .Where(a => a.CrossReferenceSource != CrossRefSource.AniDB)
                .Select(a => GetByED2K(a.ED2K)!)
                .Where(a => a != null)
                .ToList();
    }

    public IReadOnlyList<Shoko_Video> GetExactDuplicateVideos()
    {
        return
            RepoFactory.Shoko_Video_Location.GetAll()
                .GroupBy(a => a.Id)
                .Where(a => a.Count() > 1)
                .Select(a => GetByID(a.Key))
                .Where(a => a != null)
                .ToList();
    }

    public IReadOnlyList<Shoko_Video> GetIgnoredVideos()
    {
        return ReadLock(() => IsIgnoredIndex!.GetMultiple(true));
    }

    public Shoko_Video? GetByAnidbMylistId(int anidbMylistId)
    {
        return ReadLock(() => Cache.Values.FirstOrDefault(a => a.AniDBMyListId == anidbMylistId));
    }
}
