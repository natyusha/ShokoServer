using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Filters;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using FileSortCriteria = Shoko.Server.API.v3.Models.Shoko.File.FileSortCriteria;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// This Controller is intended to provide the tree. An example would be "api/v3/filter/4/group/12/series".
/// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}")]
[ApiV3]
[Authorize]
public class TreeController : BaseController
{
    private readonly FilterFactory _filterFactory;
    private readonly SeriesFactory _seriesFactory;
    private readonly FilterEvaluator _filterEvaluator;
    #region Import Folder

    /// <summary>
    /// Get all <see cref="File"/>s in the <see cref="ImportFolder"/> with the given <paramref name="folderID"/>.
    /// </summary>
    /// <param name="folderID">Import folder ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <returns></returns>
    [HttpGet("ImportFolder/{folderID}/File")]
    public ActionResult<ListResult<File>> GetFilesInImportFolder([FromRoute] int folderID,
        [FromQuery] [Range(0, 100)] int pageSize = 50,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeXRefs = false, [FromQuery] bool includeAbsolutePaths = false)
    {
        var importFolder = RepoFactory.ImportFolder.GetByID(folderID);
        if (importFolder == null)
        {
            return NotFound("Import folder not found.");
        }

        return RepoFactory.VideoLocalPlace.GetByImportFolder(importFolder.ImportFolderID)
            .GroupBy(place => place.VideoLocalID)
            .Select(places => RepoFactory.VideoLocal.GetByID(places.Key))
            .OrderBy(file => file.DateTimeCreated)
            .ToListResult(file => new File(HttpContext, file, includeXRefs, includeAbsolutePaths: includeAbsolutePaths), page, pageSize);
    }

    #endregion
    #region Filter

    /// <summary>
    /// Get a list of all the sub-<see cref="Filter"/> for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to true to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="showHidden">Show hidden filters</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Filter")]
    public ActionResult<ListResult<Filter>> GetSubFilters([FromRoute] int filterID,
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool showHidden = false)
    {
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterController.FilterNotFound);

        if (!filterPreset.IsDirectory())
            return new ListResult<Filter>();

        var hideCategories = HttpContext.GetUser().GetHideCategories();

        return _filterFactory.GetFilters(RepoFactory.FilterPreset.GetByParentID(filterID)
                .Where(filter => (showHidden || !filter.Hidden) && !hideCategories.Contains(filter.Name)).OrderBy(a => a.Name).ToList())
            .ToListResult(page, pageSize);
    }

    /// <summary>
    /// Get a paginated list of all the top-level <see cref="Group"/>s for the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="orderByName">Ignore the group filter sort critaria and always order the returned list by name.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group")]
    public ActionResult<ListResult<Group>> GetFilteredGroups([FromRoute] int filterID,
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool includeEmpty = false, [FromQuery] bool randomImages = false, [FromQuery] bool orderByName = false)
    {
        // Return the top level groups with no filter.
        IEnumerable<SVR_AnimeGroup> groups;
        if (filterID == 0)
        {
            var user = User;
            groups = RepoFactory.AnimeGroup.GetAll()
                .Where(group =>
                {
                    if (group.AnimeGroupParentID.HasValue)
                        return false;

                    if (!user.AllowedGroup(group))
                        return false;

                    return includeEmpty || group.GetAllSeries()
                        .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
                })
                .OrderBy(group => group.GetSortName());
        }
        else
        {
            var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
            if (filterPreset == null)
                return NotFound(FilterController.FilterNotFound);

            // Directories should only contain sub-filters, not groups and series.
            if (filterPreset.IsDirectory())
                return new ListResult<Group>();

            // Gets Group and Series IDs in a filter, already sorted by the filter
            var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID);
            if (!results.Any()) return new ListResult<Group>();

            groups = results
                .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key))
                .Where(group =>
                {
                    // not top level groups
                    if (group == null || group.AnimeGroupParentID.HasValue)
                        return false;

                    return includeEmpty || group.GetAllSeries()
                        .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
                });
        }

        return groups
            .ToListResult(group => new Group(HttpContext, group, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a dictionary with the count for each starting character in each of
    /// the top-level group's name with the filter for the given
    /// <paramref name="filterID"/> applied.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group/Letters")]
    public ActionResult<Dictionary<char, int>> GetGroupNameLettersInFilter([FromRoute] int? filterID = null, [FromQuery] bool includeEmpty = false)
    {
        var user = User;
        if (filterID.HasValue && filterID > 0)
        {
            var filterPreset = RepoFactory.FilterPreset.GetByID(filterID.Value);
            if (filterPreset == null)
                return NotFound(FilterController.FilterNotFound);

            // Directories should only contain sub-filters, not groups and series.
            if (filterPreset.IsDirectory())
                return new Dictionary<char, int>();

            // Fast path when user is not in the filter
            var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
            if (results.Length == 0)
                return new Dictionary<char, int>();

            return results
                .Select(group => RepoFactory.AnimeGroup.GetByID(group.Key))
                .Where(group =>
                {
                    if (group is not { AnimeGroupParentID: null })
                        return false;

                    return includeEmpty || group.GetAllSeries()
                        .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
                })
                .GroupBy(group => group.GetSortName()[0])
                .OrderBy(groupList => groupList.Key)
                .ToDictionary(groupList => groupList.Key, groupList => groupList.Count());
        }

        return RepoFactory.AnimeGroup.GetAll()
            .Where(group =>
            {
                if (group.AnimeGroupParentID.HasValue)
                    return false;

                if (!user.AllowedGroup(group))
                    return false;

                return includeEmpty || group.GetAllSeries()
                    .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
            })
            .GroupBy(group => group.GetSortName()[0])
            .OrderBy(groupList => groupList.Key)
            .ToDictionary(groupList => groupList.Key, groupList => groupList.Count());
    }

    /// <summary>
    /// Get a paginated list of all the <see cref="Series"/> within a <see cref="Filter"/>.
    /// </summary>
    /// <remarks>
    ///  Pass a <paramref name="filterID"/> of <code>0</code> to disable filter.
    /// <br/>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/>.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing
    /// <see cref="Episode"/>s in the count.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Series")]
    public ActionResult<ListResult<Series>> GetSeriesInFilteredGroup([FromRoute] int filterID,
        [FromQuery] [Range(0, 100)] int pageSize = 50, [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeMissing = false)
    {
        // Return the series with no group filter applied.
        var user = User;
        if (filterID == 0)
            return RepoFactory.AnimeSeries.GetAll()
                .Where(series => user.AllowedSeries(series) && (includeMissing || series.GetVideoLocals().Count > 0))
                .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
                .ToListResult(series => _seriesFactory.GetSeries(series, randomImages), page, pageSize);

        // Check if the group filter exists.
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterController.FilterNotFound);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory())
            return new ListResult<Series>();

        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new ListResult<Series>();

        // We don't need separate logic for ApplyAtSeriesLevel, as the FilterEvaluator handles that
        return results.SelectMany(a => a.Select(id => RepoFactory.AnimeSeries.GetByID(id)))
            .Where(series => series != null && (includeMissing || series.GetVideoLocals().Count > 0))
            .OrderBy(series => series.GetSeriesName().ToLowerInvariant())
            .ToListResult(series => _seriesFactory.GetSeries(series, randomImages), page, pageSize);
    }

    /// <summary>
    /// Get a list of all the sub-<see cref="Group"/>s belonging to the <see cref="Group"/> with the given <paramref name="groupID"/> and which are present within the <see cref="Filter"/> with the given <paramref name="filterID"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetFilteredSubGroups([FromRoute] int filterID, [FromRoute] int groupID,
        [FromQuery] bool randomImages = false, [FromQuery] bool includeEmpty = false)
    {
        // Return sub-groups with no group filter applied.
        if (filterID == 0)
            return GetSubGroups(groupID, randomImages, includeEmpty);

        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterController.FilterNotFound);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory())
            return new List<Group>();

        // Just return early because the every group will be filtered out.
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new List<Group>();
        
        // Subgroups are weird. We'll take the group, build a set of all subgroup IDs, and use that to determine if a group should be included
        // This should maintain the order of results, but have every group in the tree for those results
        var orderedGroups = results.SelectMany(a => RepoFactory.AnimeGroup.GetByID(a.Key).TopLevelAnimeGroup.GetAllChildGroups().Select(b => b.AnimeGroupID)).ToArray();
        var groups = orderedGroups.ToHashSet();
        
        return group.GetChildGroups()
            .Where(subGroup =>
            {
                if (subGroup == null)
                    return false;

                if (!user.AllowedGroup(subGroup))
                    return false;

                if (!includeEmpty && !subGroup.GetAllSeries()
                        .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0)))
                    return false;

                return groups.Contains(subGroup.AnimeGroupID);
            })
            .OrderBy(a => Array.IndexOf(orderedGroups, a.AnimeGroupID))
            .Select(g => new Group(HttpContext, g, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of all the <see cref="Series"/> for the <see cref="Group"/> within the <see cref="Filter"/>.
    /// </summary>
    /// <remarks>
    ///  Pass a <paramref name="filterID"/> of <code>0</code> to disable filter.
    /// <br/>
    /// The <see cref="Filter"/> must have <see cref="Filter.IsDirectory"/> set to false to use
    /// this endpoint.
    /// </remarks>
    /// <param name="filterID"><see cref="Filter"/> ID</param>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/>. Even the <see cref="Series"/> within the sub-<see cref="Group"/>s.</param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// /// <returns></returns>
    [HttpGet("Filter/{filterID}/Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetSeriesInFilteredGroup([FromRoute] int filterID, [FromRoute] int groupID,
        [FromQuery] bool recursive = false, [FromQuery] bool includeMissing = false,
        [FromQuery] bool randomImages = false, [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        // Return the groups with no group filter applied.
        if (filterID == 0)
            return GetSeriesInGroup(groupID, recursive, includeMissing, randomImages, includeDataFrom);

        // Check if the group filter exists.
        var filterPreset = RepoFactory.FilterPreset.GetByID(filterID);
        if (filterPreset == null)
            return NotFound(FilterController.FilterNotFound);

        if (!filterPreset.ApplyAtSeriesLevel)
            return GetSeriesInGroup(groupID, recursive, includeMissing, randomImages);

        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
            return NotFound(GroupController.GroupNotFound);

        var user = User;
        if (!user.AllowedGroup(group))
            return Forbid(GroupController.GroupForbiddenForUser);

        // Directories should only contain sub-filters, not groups and series.
        if (filterPreset.IsDirectory())
            return new List<Series>();

        // Just return early because the every series will be filtered out.
        var results = _filterEvaluator.EvaluateFilter(filterPreset, User.JMMUserID).ToArray();
        if (results.Length == 0)
            return new List<Series>();

        var seriesIDs = recursive
            ? group.GetAllChildGroups().SelectMany(a => results.FirstOrDefault(b => b.Key == a.AnimeGroupID))
            : results.FirstOrDefault(a => a.Key == groupID);

        var series = seriesIDs?.Select(a => RepoFactory.AnimeSeries.GetByID(a)).Where(a => a.GetVideoLocals().Any() || includeMissing) ??
                     Array.Empty<SVR_AnimeSeries>();

        return series
            .Select(a => _seriesFactory.GetSeries(a, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    #region Group

    /// <summary>
    /// Get a list of sub-<see cref="Group"/>s a the <see cref="Group"/>.
    /// </summary>
    /// <param name="groupID"></param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Group"/>.</param>
    /// <param name="includeEmpty">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the search.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/Group")]
    public ActionResult<List<Group>> GetSubGroups([FromRoute] int groupID, [FromQuery] bool randomImages = false,
        [FromQuery] bool includeEmpty = false)
    {
        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        if (!user.AllowedGroup(group))
        {
            return Forbid(GroupController.GroupForbiddenForUser);
        }

        return group.GetChildGroups()
            .Where(subGroup =>
            {
                if (subGroup == null)
                {
                    return false;
                }

                if (!user.AllowedGroup(subGroup))
                {
                    return false;
                }

                return includeEmpty || subGroup.GetAllSeries()
                    .Any(s => s.GetAnimeEpisodes().Any(e => e.GetVideoLocals().Count > 0));
            })
            .OrderBy(group => group.GroupName)
            .Select(group => new Group(HttpContext, group, randomImages))
            .ToList();
    }

    /// <summary>
    /// Get a list of <see cref="Series"/> within a <see cref="Group"/>.
    /// </summary>
    /// <remarks>
    /// It will return all the <see cref="Series"/> within the group and all sub-groups if
    /// <paramref name="recursive"/> is set to <code>true</code>.
    /// </remarks>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="recursive">Show all the <see cref="Series"/> within the <see cref="Group"/></param>
    /// <param name="includeMissing">Include <see cref="Series"/> with missing <see cref="Episode"/>s in the list.</param>
    /// <param name="randomImages">Randomise images shown for each <see cref="Series"/> within the <see cref="Group"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/Series")]
    public ActionResult<List<Series>> GetSeriesInGroup([FromRoute] int groupID, [FromQuery] bool recursive = false,
        [FromQuery] bool includeMissing = false, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        return (recursive ? group.GetAllSeries() : group.GetSeries())
            .Where(a => user.AllowedSeries(a))
            .OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue)
            .Select(series => _seriesFactory.GetSeries(series, randomImages, includeDataFrom))
            .Where(series => series.Size > 0 || includeMissing)
            .ToList();
    }

    /// <summary>
    /// Get the main <see cref="Series"/> in a <see cref="Group"/>.
    /// </summary>
    /// <remarks>
    /// It will return 1) the default series or 2) the earliest running
    /// series if the group contains a series, or nothing if the group is
    /// empty.
    /// </remarks>
    /// <param name="groupID"><see cref="Group"/> ID</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Group/{groupID}/MainSeries")]
    public ActionResult<Series> GetMainSeriesInGroup([FromRoute] int groupID, [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        // Check if the group exists.
        var group = RepoFactory.AnimeGroup.GetByID(groupID);
        if (group == null)
        {
            return NotFound(GroupController.GroupNotFound);
        }

        var user = User;
        if (!user.AllowedGroup(group))
        {
            return Forbid(GroupController.GroupForbiddenForUser);
        }

        var mainSeries = group.GetMainSeries();
        if (mainSeries == null)
        {
            return InternalError("Unable to find main series for group.");
        }

        return _seriesFactory.GetSeries(mainSeries, randomImages, includeDataFrom);
    }

    #endregion
    
    
    /// <summary>
    /// Get the <see cref="File"/>s for the <see cref="Series"/> with the given <paramref name="seriesID"/>.
    /// </summary>
    /// <param name="seriesID">Series ID</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/File")]
    public ActionResult<ListResult<File>> GetFilesForSeries([FromRoute] int seriesID,
    [FromQuery, Range(0, 1000)] int pageSize = 100,
    [FromQuery, Range(1, int.MaxValue)] int page = 1,
    [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
    [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[] exclude = default,
    [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[] include_only = default,
    [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string> sortOrder = null,
    [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var user = User;
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
        {
            return NotFound(SeriesController.SeriesNotFoundWithSeriesID);
        }

        if (!user.AllowedSeries(series))
        {
            return Forbid(SeriesController.SeriesForbiddenForUser);
        }

        return ModelHelper.FilterFiles(series.GetVideoLocals(), user, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
    }
    
    #region Episode

    /// <summary>
    /// Get the <see cref="File"/>s for the <see cref="Episode"/> with the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/File")]
    public ActionResult<ListResult<File>> GetFilesForEpisode([FromRoute] int episodeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[] include = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[] exclude = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[] include_only = default,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string> sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeController.EpisodeNotFoundWithEpisodeID);
        }

        var series = episode.GetAnimeSeries();
        if (series == null)
        {
            return InternalError("No Series entry for given Episode");
        }

        if (!User.AllowedSeries(series))
        {
            return Forbid(EpisodeController.EpisodeForbiddenForUser);
        }

        return ModelHelper.FilterFiles(episode.GetVideoLocals(), User, pageSize, page, include, exclude, include_only, sortOrder, includeDataFrom);
    }

    #endregion

    public TreeController(ISettingsProvider settingsProvider, FilterFactory filterFactory, FilterEvaluator filterEvaluator, SeriesFactory seriesFactory) : base(settingsProvider)
    {
        _filterFactory = filterFactory;
        _filterEvaluator = filterEvaluator;
        _seriesFactory = seriesFactory;
    }
}
