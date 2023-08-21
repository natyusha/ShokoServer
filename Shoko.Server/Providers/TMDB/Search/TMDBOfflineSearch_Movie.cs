

using Newtonsoft.Json;

namespace Shoko.Server.Providers.TMDB.Search;

public class TMDBOfflineSearch_Movie
{
    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    [JsonProperty("id")]
    public int ID = 0;

    /// <summary>
    /// Original Title in the movie's native language.
    /// </summary>
    [JsonProperty("original_title")]
    public string Title = string.Empty;

    /// <summary>
    /// Indicates that the movie is restricted to an adult audience (because
    /// it's pornographic production).
    /// </summary>
    [JsonProperty("adult")]
    public bool IsRestricted = false;

    /// <summary>
    /// Indicates that it's a video and not a movie(???).
    /// </summary>
    [JsonProperty("video")]
    public bool IsVideo = false;

    /// <summary>
    /// Global popularity ranking at the time the dumping took place.
    /// </summary>
    [JsonProperty("popularity")]
    public double Popularity = 0d;
}
