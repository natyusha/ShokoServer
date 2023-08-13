
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Custom_Tag : ITag
{
    public int Id { get; set; }

    private string _name { get; set; }

    private ITitle _preferredTitle { get; set; }

    public string Name
    {
        get
        {
            return _name;
        }
        set
        {
            _name = value;
            _preferredTitle = new TitleImpl(DataSource.User, TextLanguage.None, value, TitleType.None, true, true);
        }
    }

    private string _description { get; set; }

    private IText _preferredOverview { get; set; }

    public string Description
    {
        get
        {
            return _description;
        }
        set
        {
            _description = value;
            _preferredOverview = new TextImpl(DataSource.User, TextLanguage.None, value);
        }
    }

    public ITitle PreferredTitle =>
        _preferredTitle;

    public ITitle MainTitle =>
        _preferredTitle;

    public IReadOnlyList<ITitle> Titles =>
        new List<ITitle>() { _preferredTitle };

    public IText PreferredOverview =>
        _preferredOverview;

    public IText MainOverview =>
        _preferredOverview;

    public IReadOnlyList<IText> Overviews =>
        new List<IText>() { _preferredOverview };

    public Custom_Tag()
    {
        Id = 0;
        _name = string.Empty;
        _preferredTitle = new TitleImpl(DataSource.User, TextLanguage.None, string.Empty, TitleType.None, true, true);
        _description = string.Empty;
        _preferredOverview = new TextImpl(DataSource.User, TextLanguage.None, string.Empty);
    }

    public Custom_Tag(string name, string description)
    {
        Id = 0;
        _name = name;
        _preferredTitle = new TitleImpl(DataSource.User, TextLanguage.None, name, TitleType.None, true, true);
        _description = description;
        _preferredOverview = new TextImpl(DataSource.User, TextLanguage.None, description);
    }

    #region ITag

    string IMetadata<string>.Id => Id.ToString();

    string? ITag.ParentTagId => null;

    string ITag.TopLevelTagId => Id.ToString();

    ITag? ITag.ParentTag => null;

    ITag ITag.TopLevelTag => this;

    IReadOnlyList<ITag> ITag.ChildTags => new List<ITag>();

    bool ITag.IsSpoiler => false;

    bool ITag.IsLocalSpoiler => false;

    int? ITag.Weight => null;

    DataSource IMetadata.DataSource => DataSource.User;

    #endregion
}
