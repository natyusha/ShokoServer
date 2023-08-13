

using System;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_AnimeTitle : TitleImpl, IMetadata<int>, ITitle, IEquatable<AniDB_AnimeTitle>, IComparable, IComparable<AniDB_AnimeTitle>
{
    #region Database Columns

    public int Id { get; set; }

    public int AnimeId { get; set; }

    #endregion

    #region Helpers

    private bool? _isPreferred { get; set; }

    public override bool IsPreferred
    {
        get
        {
            if (_isPreferred.HasValue)
                return _isPreferred.Value;
            var anime = Anime;
            if (anime == null)
            {
                _isPreferred = false;
                return false;
            }
            var preferredTitle = anime.PreferredTitle;
            return Value == preferredTitle;
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

            var anime = Anime;
            if (anime == null)
            {
                _isDefault = false;
                return false;
            }

            var mainTitle = anime.MainTitle;
            _isDefault = Value == mainTitle;
            return _isDefault.Value;
        }
    }

    public AniDB_Anime? Anime =>
        RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AnimeId);

    #endregion

    public AniDB_AnimeTitle() : base(DataSource.AniDB) { }

    public AniDB_AnimeTitle(int anidbAnimeId, TextLanguage language, string value, TitleType type, bool? isDefault = null) : base(DataSource.AniDB, language, value, type, isDefault ?? false)
    {
        _isDefault = false;
        AnimeId = anidbAnimeId;
    }

}
