using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class TextImpl : IText, IEquatable<IText>, IComparable, IComparable<IText>
{
    /// <inheritdoc/>
    public virtual string? ParentId { get; set; }

    /// <inheritdoc/>
    public TextLanguage Language { get; set; }

    /// <inheritdoc/>
    public string LanguageCode
    {
        get => Language.ToLanguageCode();
        set => Language = value.ToTextLanguage();
    }

    /// <inheritdoc/>
    public string Value { get; set; }

    /// <inheritdoc/>
    public DataSource DataSource { get; }

    public TextImpl(DataSource? source = null)
    {
        ParentId = null;
        Value = string.Empty;
        Language = TextLanguage.Unknown;
        DataSource = source ?? DataSource.None;
    }

    public TextImpl(DataSource source, TextLanguage language, string value, string? parentId = null)
    {
        ParentId = parentId;
        Value = value;
        Language = language;
        DataSource = source;
    }

    public bool Equals(IText? other)
    {
        if (other == null)
            return false;
        return DataSource == other.DataSource &&
            ParentId == other.ParentId &&
            Language == other.Language &&
            Value == other.Value;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = DataSource.GetHashCode();
            hashCode = (hashCode * 397) ^ (ParentId != null ? ParentId.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Language.GetHashCode());
            hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            return hashCode;
        }
    }

    public virtual int CompareTo(object? other)
    {
        if (other == null)
            return -1;

        if (other is IText text)
            return CompareTo(text);

        return 0;
    }

    public int CompareTo(IText? other)
    {
        if (other == null)
            return -1;

        var compared = DataSource.CompareTo(other.DataSource);
        if (compared != 0)
            return compared;

        compared = string.Compare(ParentId, other.ParentId);
        if (compared != 0)
            return compared;

        compared = Language.CompareTo(other.Language);
        if (compared != 0)
            return compared;

        return string.Compare(Value, other.Value);
    }
}
