
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Shoko.Server.API.v3.Models.Common;

// For those reading the source code; yes, using the numerical values is
// possible, but that's not the preferred way to use this type.
[JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
public enum IncludeOnlyFilter
{
    /// <summary>
    /// Include nothing.
    /// </summary>
    False = 0,

    /// <summary>
    /// Include everything.
    /// </summary>
    True = 1,

    /// <summary>
    /// Include only the elements that fit the specified condition.
    /// </summary>
    Only = 2,
}
