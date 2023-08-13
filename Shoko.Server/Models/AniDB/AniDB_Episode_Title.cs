using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Episode_Title : TitleImpl, IMetadata<int>, ITitle, IEquatable<AniDB_Episode_Title>, IComparable, IComparable<AniDB_Episode_Title>
{
    #region Database Columns

    /// <summary>
    /// Local anidb episode title id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Universal anidb episode id.
    /// </summary>
    public int EpisodeId { get; set; }

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
    /// <remarks>
    /// We cache the value to re-use while the title is still in memory.
    /// </remarks>
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
    }

    private bool? _isDefault { get; set; }

    /// <inheritdoc/>
    /// <remarks>
    /// We cache the value to re-use while the title is still in memory.
    /// </remarks>
    public override bool IsDefault
    {
        get
        {
            if (_isDefault.HasValue)
                return _isDefault.Value;

            var episode = Episode;
            if (episode == null)
            {
                _isDefault = false;
                return false;
            }

            var mainTitle = episode.MainTitle;
            _isDefault = Value == mainTitle;
            return _isDefault.Value;
        }
    }

    public AniDB_Episode? Episode =>
        RepoFactory.AniDB_Episode.GetByAnidbEpisodeId(EpisodeId);

    #endregion

    #region Constructors

    public AniDB_Episode_Title() : base(DataSource.AniDB) { }

    public AniDB_Episode_Title(int anidbEpisodeId, TextLanguage language, string value, bool? isDefault = null) : base(DataSource.AniDB, language, value, TitleType.None, isDefault ?? false)
    {
        _isDefault = false;
        EpisodeId = anidbEpisodeId;
    }

    #endregion
}
