using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;

#nullable enable
namespace Shoko.Server.Databases.TypeConverters;

/// <summary>
/// Custom database type converter for NHibernate to convert between a string
/// and a hash-set for <typeparamref name="T"/>.
/// </summary>
public class StringHashSetConverter<T> : TypeConverter, IUserType
{
    #region TypeConverter

    /// <summary>
    /// Returns whether this converter can convert an object of the given type to the type of this converter.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="sourceType">A <see cref="Type"/> that represents the type you want to convert from.</param>
    /// <returns>true if this converter can perform the conversion; otherwise, false.</returns>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        // Make sure we're trying to convert from a hash-set of the correct generic type.
        if (sourceType.FullName != typeof(HashSet<T>).FullName)
            return false;

        // Check if we have a generica type, usually true if the full name match.
        if (!sourceType.ContainsGenericParameters || sourceType.GenericTypeArguments.Length != 1)
            return false;

        // Check if the name of the generic type matches the name of the type we want.
        return sourceType.GenericTypeArguments[0].Name == typeof(T).Name;
    }

    /// <summary>
    /// Returns whether this converter can convert the object to the specified type.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="destinationType">A <see cref="Type"/> that represents the type you want to convert to.</param>
    /// <returns>true if this converter can perform the conversion; otherwise, false.</returns>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        // Check if the destination type is string.
        return destinationType != null && destinationType.FullName == "System.String";
    }

    /// <summary>
    /// Converts the given object to the type of this converter, using the specified context and culture information.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="culture">The <see cref="CultureInfo"/> to use as the current culture.</param>
    /// <param name="value">The <see cref="object"/> to convert.</param>
    /// <returns>An <see cref="object"/> that represents the converted value.</returns>
    public override HashSet<T> ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
    {
        return value switch
        {
            null => new(),
            string str => str.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(MapFromString)
                .ToHashSet(),
            _ => throw new Exception("DestinationType must be string."),
        };
    }

    /// <summary>
    /// Converts the given value object to the specified type, using the specified context and culture information.
    /// </summary>
    /// <param name="context">An <see cref="ITypeDescriptorContext"/> that provides a format context.</param>
    /// <param name="culture">A <see cref="CultureInfo"/>. If null is passed, the current culture is assumed.</param>
    /// <param name="value">The <see cref="object"/> to convert.</param>
    /// <param name="destinationType">The <see cref="Type"/> to convert the value to.</param>
    /// <returns>An <see cref="object"/> that represents the converted value.</returns>
    public override string ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        // Make sure the value is set, otherwise return an empty string.
        if (value == null)
            return "";

        // Make sure we're converting from a hash-set.
        if (value is not HashSet<T> hashSet)
            throw new ArgumentException($"Value is not of type HashSet<{typeof(T).Name}>", nameof(value));

        // Filter out empty or only-white-space strings.
        var strings = hashSet
            .Select(MapToString)
            .Where(str => !string.IsNullOrWhiteSpace(str));
        return string.Join(",", strings);
    }

    private T MapFromString(string value)
    {
        switch (typeof(T).ToString())
        {
            case "System.String":
                if (value is T t1)
                    return t1;
                goto default;
            case "System.Int32":
                if (int.TryParse(value, NumberStyles.Integer, null, out var i) && i is T t2)
                    return t2;
                goto default;
            case "System.Int64":
                if (long.TryParse(value, NumberStyles.Integer, null, out var l) && l is T t3)
                    return t3;
                goto default;
            default:
                throw new Exception("");
        }
    }

    private string MapToString(T value)
    {
        return value switch
        {
            string str => str,
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentException($"Value is not of type HashSet<{typeof(T).Name}>", nameof(value)),
        };
    }

    #endregion TypeConverter
    #region IUserType

    /// <summary>
    /// Are objects of this type mutable?
    /// </summary>
    /// <value></value>
    public bool IsMutable
        => true;

    /// <summary>
    /// The type returned by <see cref="NullSafeGet"/>>
    /// </summary>
    public Type ReturnedType
        => typeof(HashSet<T>);

    /// <summary>
    /// The SQL types for the columns mapped by this type.
    /// </summary>
    /// <value></value>
    public SqlType[] SqlTypes
        => new[] { NHibernateUtil.String.SqlType };

    /// <summary>
    /// Gets the value of the HashSet&lt;<typeparamref name="T"/>&gt; from the
    /// current data reader.
    /// </summary>
    /// <param name="rs">The IDataReader to retrieve the value from.</param>
    /// <param name="names">The column names to retrieve the value from.</param>
    /// <param name="session">The current session.</param>
    /// <param name="owner">The owner object of the value.</param>
    /// <returns>The value of the HashSet&lt;<typeparamref name="T"/>&gt;.</returns>
    /// <exception cref="NHibernate.HibernateException">HibernateException</exception>
    public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
        => ConvertFrom(null, CultureInfo.InvariantCulture, NHibernateUtil.String.NullSafeGet(rs, names[0], session));

    /// <summary>
    /// Sets the value of the HashSet&lt;<typeparamref name="T"/>&gt; on the
    /// current command.
    /// </summary>
    /// <param name="cmd">The DbCommand to set the value on.</param>
    /// <param name="value">The value of the
    /// HashSet&lt;<typeparamref name="T"/>&gt;.</param>
    /// <param name="index">The index at which to set the value.</param>
    /// <param name="session">The current session.</param>
    /// <exception cref="NHibernate.HibernateException">HibernateException</exception>
    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        => NHibernateUtil.String.NullSafeSet(cmd, ConvertTo(null, CultureInfo.InvariantCulture, value, typeof(string)), index, session);

    /// <summary>
    /// Compares two instances of the HashSet&lt;<typeparamref name="T"/>&gt; to
    /// determine if they are equal.
    /// </summary>
    /// <param name="x">The first HashSet&lt;<typeparamref name="T"/>&gt; to
    /// compare.</param>
    /// <param name="y">The second HashSet&lt;<typeparamref name="T"/>&gt; to
    /// compare.</param>
    /// <returns>True if the two instances are equal, false otherwise.</returns>
    bool IUserType.Equals(object x, object y)
        => ReferenceEquals(x, y) ? true : x?.Equals(y) ?? false;

    /// <summary>
    /// Reconstruct an object from the cacheable representation. At the very least this
    /// method should perform a deep copy if the type is mutable. (optional operation)
    /// </summary>
    /// <param name="cached">the object to be cached</param>
    /// <param name="owner">the owner of the cached object</param>
    /// <returns>
    /// a reconstructed object from the cacheable representation
    /// </returns>
    public object Assemble(object cached, object owner)
        => DeepCopy(cached);

    /// <summary>
    /// Return a deep copy of the persistent state, stopping at entities and at collections.
    /// </summary>
    /// <param name="value">generally a collection element or entity field</param>
    /// <returns>a copy</returns>
    public object DeepCopy(object value)
        => new HashSet<T>((HashSet<T>)value);

    /// <summary>
    /// Transform the object into its cacheable representation. At the very least this
    /// method should perform a deep copy if the type is mutable. That may not be enough
    /// for some implementations, however; for example, associations must be cached as
    /// identifier values. (optional operation)
    /// </summary>
    /// <param name="value">the object to be cached</param>
    /// <returns>a cacheable representation of the object</returns>
    public object Disassemble(object value)
        => DeepCopy(value);

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public int GetHashCode(object x)
        => x == null ? base.GetHashCode() : x.GetHashCode();

    /// <summary>
    /// During merge, replace the existing (<paramref name="target"/>) value in the entity
    /// we are merging to with a new (<paramref name="original"/>) value from the detached
    /// entity we are merging. For immutable objects, or null values, it is safe to simply
    /// return the first parameter. For mutable objects, it is safe to return a copy of the
    /// first parameter. For objects with component values, it might make sense to
    /// recursively replace component values.
    /// </summary>
    /// <param name="original">the value from the detached entity being merged</param>
    /// <param name="target">the value in the managed entity</param>
    /// <param name="owner">the managed entity</param>
    /// <returns>the value to be merged</returns>
    public object Replace(object original, object target, object owner)
        => DeepCopy(original);

    #endregion IUserType
}
