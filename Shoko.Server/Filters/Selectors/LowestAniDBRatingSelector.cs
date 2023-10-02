using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class LowestAniDBRatingSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the lowest AniDB rating in a filterable";

    public override double Evaluate(IFilterable f)
    {
        return Convert.ToDouble(f.LowestAniDBRating);
    }

    protected bool Equals(LowestAniDBRatingSelector other)
    {
        return base.Equals(other);
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

        return Equals((LowestAniDBRatingSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(LowestAniDBRatingSelector left, LowestAniDBRatingSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(LowestAniDBRatingSelector left, LowestAniDBRatingSelector right)
    {
        return !Equals(left, right);
    }
}
