using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Plugin.Abstractions.Models.Search;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Episode : IEpisodeMetadata, IEquatable<AniDB_Episode>
{
    #region DB columns

    public int AniDB_EpisodeID { get; set; }

    public int EpisodeId { get; set; }

    public int AnimeId { get; set; }

    public int RawDuration { get; set; }

    public decimal RawRating { get; set; }

    public int Votes { get; set; }

    public int Number { get; set; }

    public AnimeType AnimeType { get; set; }

    public EpisodeType Type { get; set; }

    public string MainTitle { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public DateTime? AirDate { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    #endregion

    #region Helpers

    public TimeSpan Duration
    {
        get => TimeSpan.FromSeconds(RawDuration);
    }

    public IRating Rating
    {
        get => new RatingImpl(DataSource.AniDB, RawRating, 10, Votes);
    }

    public Shoko_Episode? GetShokoEpisode() =>
        RepoFactory.Shoko_Episode.GetByAnidbEpisodeId(EpisodeId);

    public AniDB_Anime GetAnime() =>
        RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AnimeId);

    #region Titles

    public AniDB_Episode_Title GetPreferredTitle()
    {
        // Try finding one of the preferred languages.
        var episodeTitles = GetTitles();
        foreach (var language in Languages.PreferredEpisodeNamingLanguages)
        {
            var title = episodeTitles.FirstOrDefault(title => title.Language == language.Language);
            if (title != null)
                return title;
        }

        var mainTitle = episodeTitles.FirstOrDefault(title => title.Language == TextLanguage.English && title.Value == MainTitle);
        return mainTitle ?? new(EpisodeId, TextLanguage.English, MainTitle);
    }

    public AniDB_Episode_Title GetMainTitle()
    {
        var mainTitle = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndValue(EpisodeId, MainTitle);
        if (mainTitle != null)
            mainTitle.IsDefault = true;
        return mainTitle ?? new(EpisodeId, TextLanguage.English, MainTitle, true);
    }

    public IReadOnlyList<AniDB_Episode_Title> GetTitles(IEnumerable<TextLanguage>? languages = null)
    {
        var titles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(EpisodeId);
        if (languages == null)
            return titles;

        if (!(languages is ISet<TextLanguage> languageSet))
            languageSet = languages.ToHashSet();

        if (languageSet.Count == 0)
            return new AniDB_Episode_Title[0] { };

        return titles
            .Where(title => languageSet.Contains(title.Language))
            .ToList();
    }

    #endregion

    public bool Equals(AniDB_Episode? other)
    {
        if (ReferenceEquals(other, null))
            return false;

        return EpisodeId == other.EpisodeId && AnimeId == other.AnimeId && RawDuration == other.RawDuration &&
               Rating == other.Rating && Votes == other.Votes && Number == other.Number &&
               Type == other.Type && Overview == other.Overview && AirDate == other.AirDate;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((AniDB_Episode)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = EpisodeId;
            hashCode = (hashCode * 397) ^ AnimeId;
            hashCode = (hashCode * 397) ^ RawDuration;
            hashCode = (hashCode * 397) ^ Rating.GetHashCode();
            hashCode = (hashCode * 397) ^ Votes.GetHashCode();
            hashCode = (hashCode * 397) ^ Number;
            hashCode = (hashCode * 397) ^ (int)Type;
            hashCode = (hashCode * 397) ^ (Overview != null ? Overview.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (AirDate.HasValue ? (int)AirDate.Value.Ticks : 0);
            return hashCode;
        }
    }

    #endregion

    #region IEpisodeMetadata

    string IMetadata<string>.Id =>
        EpisodeId.ToString();

    string? IEpisodeMetadata.SeasonId =>
        null;

    string IEpisodeMetadata.ShowId =>
        AnimeId.ToString();

    IReadOnlyList<int> IEpisodeMetadata.ShokoEpisodeIds
    {
        get
        {
            var shokoEpisode = GetShokoEpisode();
            return shokoEpisode == null ? new int[0] { } : new int[1] { shokoEpisode.Id };
        }
    }

    ITitle ITitleContainer.PreferredTitle =>
        GetPreferredTitle();

    ITitle ITitleContainer.MainTitle =>
        GetMainTitle();

    IReadOnlyList<ITitle> ITitleContainer.Titles =>
        GetTitles();

    IText IOverviewContainer.PreferredOverview =>
        new TextImpl(DataSource.AniDB, TextLanguage.English, Overview);

    IText IOverviewContainer.MainOverview =>
        new TextImpl(DataSource.AniDB, TextLanguage.English, Overview);

    IReadOnlyList<IText> IOverviewContainer.Overviews =>
        new IText[] { new TextImpl(DataSource.AniDB, TextLanguage.English, Overview) };

    int? IEpisodeMetadata.SeasonNumber =>
        null;

    int? IEpisodeMetadata.AbsoluteNumber =>
        Number;

    int? IEpisodeMetadata.AirsAfterSeason =>
        null;

    int? IEpisodeMetadata.AirsBeforeEpisode =>
        null;

    int? IEpisodeMetadata.AirsBeforeSeason =>
        null;

    ISeasonMetadata? IEpisodeMetadata.Season =>
        null;

    IShowMetadata IEpisodeMetadata.Show =>
        GetAnime();

    IReadOnlyList<IShokoEpisode> IEpisodeMetadata.ShokoEpisodes
    {
        get
        {
            var shokoEpisode = GetShokoEpisode();
            return shokoEpisode == null ? new IShokoEpisode[0] { } : new IShokoEpisode[1] { shokoEpisode };
        }
    }

    IImageMetadata? IImageContainer.PreferredImage =>
        null;

    IReadOnlyList<IImageMetadata> IImageContainer.AllImages =>
        new IImageMetadata[0] { };

    IReadOnlyList<IImageMetadata> IImageContainer.GetImages(ImageMetadataSearchOptions? options) =>
        new IImageMetadata[0] { };

    DataSource IMetadata.DataSource =>
        DataSource.AniDB;

    #endregion
}

