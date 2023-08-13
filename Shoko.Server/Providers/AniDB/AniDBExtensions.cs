using AbstractAnimeType = Shoko.Plugin.Abstractions.Enums.AnimeType;
using AbstractEpisodeType = Shoko.Plugin.Abstractions.Enums.EpisodeType;

namespace Shoko.Server.Providers.AniDB;

public static class AniDBExtensions
{
    public static AbstractAnimeType ToAbstraction(this AnimeType type)
        => type switch
        {
            AnimeType.Movie => AbstractAnimeType.Movie,
            AnimeType.OVA => AbstractAnimeType.OVA,
            AnimeType.TVSeries => AbstractAnimeType.TV,
            AnimeType.TVSpecial => AbstractAnimeType.TVSpecial,
            AnimeType.Web => AbstractAnimeType.Web,
            AnimeType.Other => AbstractAnimeType.Other,
            _ => AbstractAnimeType.None,
        };

    public static AbstractEpisodeType ToAbstraction(this EpisodeType type)
        => type switch
        {
            EpisodeType.Episode => AbstractEpisodeType.Normal,
            EpisodeType.Credits => AbstractEpisodeType.ThemeSong,
            EpisodeType.Special => AbstractEpisodeType.Special,
            EpisodeType.Trailer => AbstractEpisodeType.Trailer,
            EpisodeType.Parody => AbstractEpisodeType.Parody,
            EpisodeType.Other => AbstractEpisodeType.Other,
            _ => AbstractEpisodeType.Unknown,
        };
}
