using Shoko.Plugin.Abstractions.Models;

namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime;

public class ResponseTitle
{
    public TitleType TitleType { get; set; }
    public TextLanguage Language { get; set; }
    public string Title { get; set; }
}
