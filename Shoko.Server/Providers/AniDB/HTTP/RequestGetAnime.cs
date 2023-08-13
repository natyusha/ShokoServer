﻿using System;
using Microsoft.Extensions.Logging;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class RequestGetAnime : HttpRequest<ResponseGetAnime>
{
    private readonly HttpXmlUtils _xmlUtils;
    private readonly HttpAnimeParser _parser;
    private readonly string _aniDBUrl;
    private readonly ushort _aniDBPort;

    public int AnimeID { get; set; }

    protected override string BaseCommand =>
        $"http://{_aniDBUrl}:{_aniDBPort}/httpapi?client=animeplugin&clientver=1&protover=1&request=anime&aid={AnimeID}";

    public RequestGetAnime(IHttpConnectionHandler handler, ILoggerFactory loggerFactory, HttpXmlUtils xmlUtils,
        HttpAnimeParser parser, ISettingsProvider settingsProvider) : base(handler, loggerFactory)
    {
        _xmlUtils = xmlUtils;
        _parser = parser;
        var settings = settingsProvider.GetSettings().AniDb;
        _aniDBUrl = settings.ServerAddress;
        _aniDBPort = (ushort)(settings.ServerPort + 1);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="receivedData"></param>
    /// <returns></returns>
    /// <exception cref="AniDBBannedException">Will throw if banned. Won't extend ban, so it's safe to use this as a check</exception>
    protected override HttpResponse<ResponseGetAnime> ParseResponse(HttpResponse<string> receivedData)
    {
        UpdateAccessTime(AnimeID);

        // save a file cache of the response
        var rawXml = receivedData.Response.Trim();
        _xmlUtils.WriteAnimeHTTPToFile(AnimeID, rawXml);

        var response = _parser.Parse(AnimeID, receivedData.Response);
        return new HttpResponse<ResponseGetAnime> { Code = receivedData.Code, Response = response };
    }

    private static void UpdateAccessTime(int animeId)
    {
        // Putting this here for no chance of error. It is ALWAYS created or updated when AniDB is called!
        var anime = RepoFactory.AniDB_Anime_Update.GetByAnimeID(animeId);
        if (anime == null)
        {
            anime = new AniDB_AnimeUpdate { AnimeID = animeId, UpdatedAt = DateTime.Now };
        }
        else
        {
            anime.UpdatedAt = DateTime.Now;
        }

        RepoFactory.AniDB_Anime_Update.Save(anime);
    }
}
