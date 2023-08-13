
using System.Collections;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ShokoEpisode_Title : TitleImpl, IMetadata<int>, ITitle
{
    #region Database Columns

    /// <summary>
    /// Local shoko episode title id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Local shoko episode id.
    /// </summary>
    public int EpisodeId { get; set; }

    /// <summary>
    /// Universal anidb episode id.
    /// </summary>
    public int AnidbEpisodeId { get; set; }

    /// <summary>
    /// The id of the user that added this title.
    /// </summary>
    public int UserId { get; set; }

    #endregion

    #region Helpers

    public override string? ParentId
    {
        get => EpisodeId.ToString();
        set
        {
            if (int.TryParse(value, out var episodeId))
            {
                EpisodeId = episodeId;
                base.ParentId = value;
            }
        }
    }

    private bool? _isPreferred { get; set; }

    /// <inheritdoc/>
    public override bool IsPreferred
    {
        get
        {
            if (_isPreferred.HasValue)
                return _isPreferred.Value;
            var episode = Episode;
            if (episode == null)
            {
                _isPreferred = false;
                return false;
            }
            var preferredTitle = episode.GetPreferredTitle();
            _isPreferred = Equals(preferredTitle);
            return _isPreferred.Value;
        }
        set => _isPreferred = value;
    }

    public override bool IsDefault { get => false; }

    public Shoko_Episode? Episode =>
        RepoFactory.Shoko_Episode.GetByID(EpisodeId);

    public Shoko_User? User =>
        RepoFactory.Shoko_User.GetByID(UserId);

    #endregion

    #region Constructors

    public ShokoEpisode_Title() : base(DataSource.Shoko) { }

    public ShokoEpisode_Title(int episodeId, int userId, TextLanguage language, string value, TitleType type = TitleType.None) : base(DataSource.Shoko, language, value, type)
    {
        EpisodeId = episodeId;
        UserId = userId;
    }

    #endregion

    #region IEqualityComparer<ShokoEpisodeTitle>

    public bool Equals(ShokoEpisode_Title? other) =>
        Equals(this, other);

    public bool Equals(ShokoEpisode_Title? first, ShokoEpisode_Title? second)
    {
        if (first == null && second == null)
            return true;
        if (first == null || second == null)
            return false;
        return first.EpisodeId == second.EpisodeId &&
            first.UserId == second.UserId &&
            first.Language == second.Language &&
            first.Type == second.Type &&
            first.Value == second.Value;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = EpisodeId.GetHashCode();
            hashCode = (hashCode * 397) ^ (UserId.GetHashCode());
            hashCode = (hashCode * 397) ^ (Language.GetHashCode());
            hashCode = (hashCode * 397) ^ (Type.GetHashCode());
            hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            return hashCode;
        }
    }

    #endregion

    #region IComparer<ShokoEpisodeTitle>



    #endregion

}
