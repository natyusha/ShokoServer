
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Shoko.Server.API.v3.Models.Common;

// For those reading the source code; yes, using the numerical values is
// possible, but that's not the preferred way to use this type.
[JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
public enum IncludeVideoXRefs
{
    /// <summary>
    /// Include nothing.
    /// </summary>
    False = 0,

    /// <summary>
    /// Use the old implementation.
    /// </summary>
    True = 1,

    /// <summary>
    /// Use the old implementation.
    /// </summary>
    V1 = True,

    /// <summary>
    /// Use the new implementation.
    /// </summary>
    V2 = 2,
}
