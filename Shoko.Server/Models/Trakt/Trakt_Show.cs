using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Server.Models.Trakt;

public class Trakt_Show : IShowMetadata
{
    public int Id { get; set; }

    public string TraktShowID { get; set; }

    public int? TvdbShowId { get; set; }
    
    public int? TmdbShowId { get; set; }

    public string MainTitle { get; set; }

    public string MainOverview { get; set; }

    public string Year { get; set; }

    public string URL { get; set; }
}
