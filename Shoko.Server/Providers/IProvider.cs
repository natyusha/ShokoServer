
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Models;

#nullable enable
namespace Shoko.Server.Providers;

public interface IMovieMetadataProvider<TMovieMetadata, TSearchMovieMetadata, TProviderID> where TMovieMetadata : IMovieMetadata
{
    public IReadOnlyList<TSearchMovieMetadata> SearchMovieMetadata(string title);

    public TMovieMetadata? GetMovieMetadata(TProviderID providerMovieID);

    public void RefreshMovieMetadata(TProviderID providerMovieID);

    public IReadOnlyList<TMovieMetadata> GetMovieMetadataForAnime(int anidbAnimeID);

    public virtual bool AddMovieLink(int anidbAnimeID, TProviderID providerMovieID, bool replaceExisting = false)
        => AddMovieLinks(anidbAnimeID, new TProviderID[] { providerMovieID }, replaceExisting) == 1;

    public int AddMovieLinks(int anidbAnimeID, IReadOnlyList<TProviderID> providerMovieIDs, bool replaceExisting = false);

    public virtual bool RemoveMovieLink(int anidbAnimeID, TProviderID providerMovieID)
        => RemoveMovieLinks(anidbAnimeID, new TProviderID[] { providerMovieID }) == 1;

    public int RemoveMovieLinks(int anidbAnimeID, IReadOnlyList<TProviderID>? providerMovieIDs = null);
}

public interface IShowMetadataProvider<TShowMetadata, TSearchShowMetadata, TProviderID> where TShowMetadata : IShowMetadata
{
    public IReadOnlyList<TShowMetadata> SearchShowMetadata(string title);

    public TShowMetadata? GetShowMetadata(TProviderID providerShowID);

    public void RefreshShowMetadata(TProviderID providerMovieID);

    public IReadOnlyList<TShowMetadata> GetShowMetadataForAnime(int anidbAnimeID);

    public bool AddShowLink(int anidbAnimeID, TProviderID providerShowID, bool replaceExisting = false)
        => AddShowLinks(anidbAnimeID, new TProviderID[] { providerShowID }, replaceExisting) == 1;

    public int AddShowLinks(int anidbAnimeID, IReadOnlyList<TProviderID> providerShowIDs, bool replaceExisting = false);

    public virtual bool RemoveShowLink(int anidbAnimeID, TProviderID providerShowID)
        => RemoveShowLinks(anidbAnimeID, new TProviderID[] { providerShowID }) == 1;

    public int RemoveShowLinks(int anidbAnimeID, IReadOnlyList<TProviderID>? providerShowIDs = null);
    
}
