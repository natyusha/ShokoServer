﻿using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached;

public class AnimeGroupRepository : BaseCachedRepository<SVR_AnimeGroup, int>
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    private PocoIndex<int, SVR_AnimeGroup, int> Parents;

    private ChangeTracker<int> Changes = new();

    public AnimeGroupRepository()
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByGroupID(cr.AnimeGroupID));
        };
        EndDeleteCallback = cr =>
        {
            if (cr.AnimeGroupParentID.HasValue && cr.AnimeGroupParentID.Value > 0)
            {
                logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}",
                    cr.AnimeGroupParentID.Value);
                var ngrp = GetByID(cr.AnimeGroupParentID.Value);
                if (ngrp != null)
                {
                    Save(ngrp, false, true);
                }
            }
        };
    }

    protected override int SelectKey(SVR_AnimeGroup entity)
    {
        return entity.AnimeGroupID;
    }

    public override void PopulateIndexes()
    {
        Changes.AddOrUpdateRange(Cache.Keys);
        Parents = Cache.CreateIndex(a => a.AnimeGroupParentID ?? 0);
    }

    public override void RegenerateDb()
    {
        var grps = Cache.Values.Where(a => a.ContractVersion < SVR_AnimeGroup.CONTRACT_VERSION)
            .ToList();
        var max = grps.Count;
        var cnt = 0;
        ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
            typeof(AnimeGroup).Name, " DbRegen");
        if (max <= 0)
        {
            return;
        }

        foreach (var g in grps)
        {
            g.Description = g.Description?.Replace('`', '\'');
            g.GroupName = g.GroupName?.Replace('`', '\'');
            Save(g, true, false, false);
            cnt++;
            if (cnt % 10 == 0)
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, typeof(AnimeGroup).Name,
                    " DbRegen - " + cnt + "/" + max);
            }
        }

        ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
            typeof(AnimeGroup).Name,
            " DbRegen - " + max + "/" + max);
    }

    public override void Save(SVR_AnimeGroup obj)
    {
        Save(obj, true, true);
    }

    public void Save(SVR_AnimeGroup grp, bool updategrpcontractstats, bool recursive,
        bool verifylockedFilters = true)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var sessionWrapper = session.Wrap();
        Lock(session, s =>
        {
            //We are creating one, and we need the AnimeGroupID before Update the contracts
            if (grp.AnimeGroupID == 0)
            {
                grp.Contract = null;
                using var transaction = s.BeginTransaction();
                s.SaveOrUpdate(grp);
                transaction.Commit();
            }
        });

        UpdateCache(grp);
        var types = grp.UpdateContract(sessionWrapper, updategrpcontractstats);
        Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            SaveWithOpenTransaction(s, grp);
            transaction.Commit();
        });

        Changes.AddOrUpdate(grp.AnimeGroupID);

        if (verifylockedFilters)
        {
            RepoFactory.FilterPreset.CreateOrVerifyDirectoryFilters(false, grp.Contract?.Stat_AllTags,
                grp.Contract?.Stat_AllYears, grp.Contract?.GetSeasons());
        }

        if (grp.AnimeGroupParentID.HasValue && recursive)
        {
            var pgroup = GetByID(grp.AnimeGroupParentID.Value);
            // This will avoid the recursive error that would be possible, it won't update it, but that would be
            // the least of the issues
            if (pgroup != null && pgroup.AnimeGroupParentID == grp.AnimeGroupID)
            {
                Save(pgroup, updategrpcontractstats, true, verifylockedFilters);
            }
        }
    }

    public void InsertBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeGroup> groups)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (groups == null)
        {
            throw new ArgumentNullException(nameof(groups));
        }

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            session.Insert(group);
            UpdateCache(group);
        }
        trans.Commit();

        Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
    }

    public void UpdateBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeGroup> groups)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (groups == null)
        {
            throw new ArgumentNullException(nameof(groups));
        }

        using var trans = session.BeginTransaction();
        foreach (var group in groups)
        {
            session.Update(group);
            UpdateCache(group);
        }
        trans.Commit();

        Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
    }

    /// <summary>
    /// Deletes all AnimeGroup records.
    /// </summary>
    /// <remarks>
    /// This method also makes sure that the cache is cleared.
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="excludeGroupId">The ID of the AnimeGroup to exclude from deletion.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
    public void DeleteAll(ISessionWrapper session, int? excludeGroupId = null)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        // First, get all of the current groups so that we can inform the change tracker that they have been removed later
        var allGrps = GetAll();

        Lock(() =>
        {
            // Then, actually delete the AnimeGroups
            if (excludeGroupId != null)
            {
                session.CreateQuery("delete SVR_AnimeGroup ag where ag.id <> :excludeId")
                    .SetInt32("excludeId", excludeGroupId.Value)
                    .ExecuteUpdate();
            }
            else
            {
                session.CreateQuery("delete SVR_AnimeGroup ag")
                    .ExecuteUpdate();
            }
        });

        if (excludeGroupId != null)
        {
            Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID)
                .Where(id => id != excludeGroupId.Value));
        }
        else
        {
            Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID));
        }

        // Finally, we need to clear the cache so that it is in sync with the database
        ClearCache();

        // If we're excluding a group from deletion, and it was in the cache originally, then re-add it back in
        if (excludeGroupId != null)
        {
            var excludedGroup = allGrps.FirstOrDefault(g => g.AnimeGroupID == excludeGroupId.Value);

            if (excludedGroup != null)
            {
                UpdateCache(excludedGroup);
            }
        }
    }

    public List<SVR_AnimeGroup> GetByParentID(int parentid)
    {
        return ReadLock(() => Parents.GetMultiple(parentid));
    }

    public List<SVR_AnimeGroup> GetAllTopLevelGroups()
    {
        return GetByParentID(0);
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }
}
