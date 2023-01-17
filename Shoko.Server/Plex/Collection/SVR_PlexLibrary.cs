using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Models.Plex;
using Shoko.Models.Plex.Collection;
using Shoko.Server.Plex.TVShow;
using MediaContainer = Shoko.Models.Plex.TVShow.MediaContainer;

namespace Shoko.Server.Plex.Collection;

public class SVR_PlexLibrary : PlexLibrary
{
    public SVR_PlexLibrary(PlexHelper helper)
    {
        Helper = helper;
    }

    private PlexHelper Helper { get; }

    public List<SVR_Episode> GetEpisodes()
    {
        var (_, data) = Helper.RequestFromPlex($"/library/metadata/{RatingKey}/allLeaves").GetAwaiter()
            .GetResult();
        return JsonConvert
            .DeserializeObject<MediaContainer<MediaContainer>>(data, Helper.SerializerSettings)
            .Container?.Metadata?
            .Where(episode => episode != null)
            .Cast<SVR_Episode>()
            .ToList() ?? new();
    }
}
