using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TMDB_Search)]
public class CommandRequest_TMDB_Search : CommandRequestImplementation
{
    private readonly TMDBHelper _helper;
    private readonly ISettingsProvider _settingsProvider;
    public virtual int AnimeID { get; set; }
    public virtual bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Searching for anime on TMDB: {0}",
        queueState = QueueStateEnum.SearchTMDb,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Search: {AnimeID}", AnimeID);

        // Use TvDB setting
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoLink)
        {
            return;
        }

        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null)
        {
            return;
        }

        if (anime.AnimeType == (int)AnimeType.Movie)
        {
            SearchForMovies(anime);
            return;
        }

        SearchForShows(anime);
    }

    private void SearchForMovies(SVR_AniDB_Anime anime)
    {
        var searchCriteria = anime.PreferredTitle;

        // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
        var results = _helper.SearchMovies(searchCriteria);
        Logger.LogTrace("Found {Count} results for {Criteria} on TMDB", results.Count, searchCriteria);
        if (ProcessSearchResults(results, searchCriteria))
        {
            return;
        }


        if (results.Count != 0)
        {
            return;
        }

        foreach (var title in anime.GetTitles())
        {
            if (title.TitleType != TitleType.Official)
            {
                continue;
            }

            if (string.Equals(searchCriteria, title.Title, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            results = _helper.SearchMovies(title.Title);
            Logger.LogTrace("Found {Count} results for search on {Title}", results.Count, title.Title);
            if (ProcessSearchResults(results, title.Title))
            {
                return;
            }
        }
    }

    private void SearchForShows(SVR_AniDB_Anime anime)
    {
        // TODO: For later.
    }

    private bool ProcessSearchResults(List<TMDB_Movie_Result> results, string searchCriteria)
    {
        if (results.Count == 1)
        {
            // since we are using this result, lets download the info
            Logger.LogTrace("Found 1 results for search on {SearchCriteria} --- Linked to {Name} ({ID})",
                searchCriteria,
                results[0].MovieName, results[0].MovieID);

            var movieID = results[0].MovieID;
            _helper.UpdateMovie(movieID, true);
            _helper.AddMovieLink(AnimeID, movieID, isAutomatic: true);
            return true;
        }

        return false;
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Search{AnimeID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Search), "AnimeID"));
        ForceRefresh =
            bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Search), "ForceRefresh"));

        return true;
    }

    public CommandRequest_TMDB_Search(ILoggerFactory loggerFactory, TMDBHelper helper, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_TMDB_Search()
    {
    }
}
