using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Models.Plex;
using Shoko.Models.Plex.Libraries;
using Shoko.Server.Plex.Collection;
using MediaContainer = Shoko.Models.Plex.Collection.MediaContainer;

namespace Shoko.Server.Plex.Libraries;

public class SVR_Directory : Directory
{
    public SVR_Directory(PlexHelper helper)
    {
        Helper = helper;
    }

    private PlexHelper Helper { get; }

    public List<SVR_PlexLibrary> GetShows()
    {
        var (_, json) = Helper.RequestFromPlex($"/library/sections/{Key}/all").ConfigureAwait(false)
            .GetAwaiter().GetResult();
        return JsonConvert
            .DeserializeObject<MediaContainer<MediaContainer>>(json, Helper.SerializerSettings)
            .Container?.Metadata?
            .Where(library => library != null)
            .Cast<SVR_PlexLibrary>()
            .ToList() ?? new();
    }
}
