using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using TMDbLib.Objects.General;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Company
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_CompanyID { get; set; }

    /// <summary>
    /// TMDB Company ID.
    /// </summary>
    public int TmdbCompanyID { get; set; }

    /// <summary>
    /// Main name of the company on TMDB.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The country the company originates from.
    /// </summary>
    public string CountryOfOrigin { get; set; } = string.Empty;

    #endregion

    #region Constructors

    public TMDB_Company() { }

    public TMDB_Company(int companyId)
    {
        TmdbCompanyID = companyId;
    }

    #endregion

    #region Methods

    public bool Populate(ProductionCompany company)
    {
        var updated = false;
        if (!string.IsNullOrEmpty(company.Name) && !string.Equals(company.Name, Name))
        {
            Name = company.Name;
            updated = true;
        }
        if (!string.IsNullOrEmpty(company.OriginCountry) && !string.Equals(company.OriginCountry, CountryOfOrigin))
        {
            CountryOfOrigin = company.OriginCountry;
            updated = true;
        }
        return updated;
    }

    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbCompanyIDAndType(TmdbCompanyID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbCompanyID(TmdbCompanyID);

    public IReadOnlyList<TMDB_Company_Entity> GetTmdbCompanyCrossReferences() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(TmdbCompanyID);

    public IReadOnlyList<IEntityMetadata> GetTmdbEntities() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbEntity())
            .OfType<IEntityMetadata>()
            .ToList();

    public IReadOnlyList<IEntityMetadata> GetTmdbShows() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbShow())
            .OfType<TMDB_Show>()
            .ToList();

    public IReadOnlyList<IEntityMetadata> GetTmdbMovies() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbMovie())
            .OfType<TMDB_Movie>()
            .ToList();

    #endregion
}
