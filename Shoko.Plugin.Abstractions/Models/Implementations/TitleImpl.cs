using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class TitleImpl : TextImpl, ITitle, IEquatable<ITitle>, IComparable, IComparable<ITitle>
{
    /// <inheritdoc/>
    public virtual bool IsPreferred { get; set; }

    /// <inheritdoc/>
    public virtual bool IsDefault { get; set; }

    /// <inheritdoc/>
    public TitleType Type { get; }

    public TitleImpl(DataSource? source = null) : base(source)
    {
        IsPreferred = false;
        Type = TitleType.None;
    }

    public TitleImpl(DataSource source, TextLanguage language, string value, TitleType type, bool isDefault = false, bool isPreferred = false) : base(source, language, value)
    {
        IsDefault = isDefault;
        IsPreferred = isPreferred;
        Type = type;
    }

    public bool Equals(ITitle? other)
    {
        if (other == null)
            return false;
        return DataSource == other.DataSource &&
            ParentId == other.ParentId &&
            Type == other.Type &&
            Language == other.Language &&
            Value == other.Value;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = DataSource.GetHashCode();
            hashCode = (hashCode * 397) ^ (ParentId != null ? ParentId.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Type.GetHashCode());
            hashCode = (hashCode * 397) ^ (Language.GetHashCode());
            hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            return hashCode;
        }
    }

    public override int CompareTo(object? other)
    {
        if (other is ITitle title)
            return CompareTo(title);

        return base.CompareTo(other);
    }

    public int CompareTo(ITitle? other)
    {
        if (other == null)
            return -1;

        var compared = DataSource.CompareTo(other.DataSource);
        if (compared != 0)
            return compared;

        compared = string.Compare(ParentId, other.ParentId);
        if (compared != 0)
            return compared;

        compared = Type.CompareTo(other.Type);
        if (compared != 0)
            return compared;

        compared = Language.CompareTo(other.Language);
        if (compared != 0)
            return compared;

        return string.Compare(Value, other.Value);
    }
}
