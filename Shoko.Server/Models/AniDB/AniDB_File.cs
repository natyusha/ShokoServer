using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.AniDB;

public class AniDB_File : IAniDBFile
{
    #region Database Columns

    public int Id { get; set; }

    public int AnidbFileId { get; set; }

    public int ReleaseGroupId { get; set; }

    public string ED2K { get; set; }

    public string RawSource { get; set; }

    public string Comment { get; set; }

    public string OriginalFileName { get; set; }

    public long FileSize { get; set; }

    public int FileVersion { get; set; }

    public bool? IsCensored { get; set; }

    public bool IsDeprecated { get; set; }

    public bool IsChaptered { get; set; }

    public int InternalVersion { get; set; }

    public DateTime ReleasedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Helpers

    public FileSource Source =>
        RawSource.ToFileSource();

    public int? VideoId =>
        Video?.Id;

    public Shoko_Video Video =>
        RepoFactory.Shoko_Video.GetByED2K(ED2K);

    public IReadOnlyList<TextLanguage> AudioLanguages =>
        RepoFactory.CR_AniDB_File_Languages.GetByFileID(Id)
            .Select(xref => xref.LanguageName.ToTextLanguage())
            .ToList();

    public IReadOnlyList<TextLanguage> TextLanguages =>
        RepoFactory.CR_AniDB_File_Subtitles.GetByFileID(Id)
            .Select(xref => xref.LanguageName.ToTextLanguage())
            .ToList();

    public IReadOnlyList<CR_Video_Episode> CrossReferences =>
        RepoFactory.CR_Video_Episode.GetByED2K(ED2K)
            .Where(xref => xref.CrossReferenceSource == Shoko.Models.Enums.CrossRefSource.AniDB)
            .ToList();

    public AniDB_ReleaseGroup ReleaseGroup
    {
        get
        {
            var releaseGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(ReleaseGroupId);
            if (releaseGroup != null)
                return releaseGroup;

            return new() { GroupID = ReleaseGroupId };
        }
    }

    public string Anime_GroupName => ReleaseGroup?.Name;

    public string Anime_GroupNameShort => ReleaseGroup?.ShortName;

    #endregion

    #region Links

    #endregion

    bool IAniDBFile.IsCensored =>
        IsCensored ?? false;

    IReleaseGroup IAniDBFile.ReleaseGroup =>
        ReleaseGroup;

    IShokoVideo IAniDBFile.Video =>
        Video;

    IAniDBMediaInfo IAniDBFile.Media =>
        new AniDBMediaInfoImpl(AudioLanguages, TextLanguages);
}
