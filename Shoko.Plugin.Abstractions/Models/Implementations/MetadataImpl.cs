
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class MetadataImpl<TId> : IMetadata<TId>
{
    public TId Id { get; }

    public DataSource DataSource { get; }

    public MetadataImpl(TId id, DataSource dataSource)
    {
        Id = id;
        DataSource = dataSource;
    }
}
