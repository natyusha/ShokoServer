using System;
using System.IO;
using System.Linq;
using Shoko.Models.Plex.TVShow;

namespace Shoko.Server.Plex.TVShow;

public class SVR_Episode : Episode
{
    private PlexHelper Helper { get; set; }

    public SVR_Episode(PlexHelper helper)
    {
        Helper = helper;
    }

    public string FileName
        => Path.GetFileName(Media[0].Part[0].File);

    public void Unscrobble()
    {
        Helper.RequestFromPlex($"/:/unscrobble?identifier=com.plexapp.plugins.library&key={RatingKey}")
            .GetAwaiter().GetResult();
    }

    public void Scrobble()
    {
        Helper.RequestFromPlex($"/:/scrobble?identifier=com.plexapp.plugins.library&key={RatingKey}")
            .GetAwaiter().GetResult();
    }
}
