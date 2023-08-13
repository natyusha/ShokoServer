using Shoko.Models.Enums;

#nullable enable
namespace Shoko.Server.Models.CrossReferences;

public class CR_AniDB_TvDB_Episode
{
    public int Id { get; set; }

    public int AnidbEpisodeId { get; set; }

    public int TvdbEpisodeId { get; set; }

    public MatchRating MatchRating { get; set; } = MatchRating.SarahJessicaParker;

    public CrossRefSource Source { get; set; } = CrossRefSource.Automatic;
}

