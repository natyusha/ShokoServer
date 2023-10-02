using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class FilterExpression : IFilterExpression
{
    [IgnoreDataMember] [JsonIgnore] public virtual bool TimeDependent => false;
    [IgnoreDataMember] [JsonIgnore] public virtual bool UserDependent => false;
    [IgnoreDataMember] [JsonIgnore] public virtual string HelpDescription => string.Empty;
    [IgnoreDataMember] [JsonIgnore] public virtual string[] HelpPossibleParameters => Array.Empty<string>();
    [IgnoreDataMember] [JsonIgnore] public virtual string[] HelpPossibleSecondParameters => Array.Empty<string>();

    protected virtual bool Equals(FilterExpression other)
    {
        return true;
    }

    public override bool Equals(object obj)
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

        return Equals((FilterExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(FilterExpression left, FilterExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(FilterExpression left, FilterExpression right)
    {
        return !Equals(left, right);
    }

    public virtual bool IsType(FilterExpression expression)
    {
        return expression.GetType() == GetType();
    }
}

public abstract class FilterExpression<T> : FilterExpression, IFilterExpression<T>
{
    public abstract T Evaluate(IFilterable f);
}
