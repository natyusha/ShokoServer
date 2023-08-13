using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Rating object. Shared between sources, episodes vs series, etc
/// </summary>
public class Rating
{
    /// <summary>
    /// rating
    /// </summary>
    [Required]
    public decimal Value { get; set; }

    /// <summary>
    /// out of what? Assuming int, as the max should be
    /// </summary>
    [Required]
    public int MaxValue { get; set; }

    /// <summary>
    /// AniDB, etc
    /// </summary>
    [Required]
    public string Source { get; set; }

    /// <summary>
    /// number of votes
    /// </summary>
    public int Votes { get; set; }

    /// <summary>
    /// for temporary vs permanent, or any other situations that may arise later
    /// </summary>
    public string Type { get; set; }

    public Rating(IRating rating)
    {
        Value = rating.Value;
        MaxValue = rating.MaxValue;
        Source = rating.DataSource.ToString();
        Votes = rating.Votes ?? 0;
        Type = rating.Type;
    }

    public Rating(AniDB_Anime anime)
    {
        Source = "AniDB";
        Value = anime.Rating;
        MaxValue = 1000;
        Votes = anime.VoteCount;
    }

    public Rating(AniDB_Anime_Similar similar)
    {
        Value = new Vote(similar.Approval, similar.Total).GetRating(100);
        MaxValue = 100;
        Votes = similar.Total;
        Source = "AniDB";
        Type = "User Approval";
    }
    
    public Rating(AniDB_Vote vote)
    {
        var voteType = (AniDBVoteType)vote.VoteType == AniDBVoteType.Anime ? "Permanent" : "Temporary";
        Value = (decimal)Math.Round(vote.VoteValue / 100D, 1);
        MaxValue = 10;
        Type = voteType;
        Source = "User";
    }
}
