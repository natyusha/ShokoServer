using Shoko.Models.Enums;

namespace Shoko.Server.Models.CrossReferences;

public class CR_AniDB_Trakt
{
    public int Id { get; set; }

    public int AnimeID { get; set; }

    public string TraktID { get; set; }

    public int AniDBStartEpisodeType { get; set; }

    public int AniDBStartEpisodeNumber { get; set; }

    public int TraktSeasonNumber { get; set; }

    public int TraktStartEpisodeNumber { get; set; }

    public string TraktTitle { get; set; }

    public MatchRating MatchRating { get; set; } = MatchRating.SarahJessicaParker;

    public CrossRefSource Source { get; set; } = CrossRefSource.Automatic;
}
