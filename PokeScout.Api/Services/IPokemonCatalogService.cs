using PokeScout.Api.Dtos;

namespace PokeScout.Api.Services;

public interface IPokemonCatalogService
{
    Task<List<CatalogSearchResultDto>> SearchByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<CatalogSetListItemDto>> GetSetsAsync(CancellationToken cancellationToken = default);
    Task<List<CatalogSetCardDto>> GetCardsBySetAsync(string setId, CancellationToken cancellationToken = default);
    Task<CatalogImageResult> GetSetImageAsync(string setId, CancellationToken cancellationToken = default);
    Task<CatalogImageResult> GetImageAsync(string id, string size = "high", CancellationToken cancellationToken = default);

    Task<CatalogSetImportResultDto> ImportSetToCatalogAsync(
        string setId,
        bool warmImages = false,
        int? maxImagesToWarm = null,
        CancellationToken cancellationToken = default);
}

public sealed class CatalogSetListItemDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Series { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public int? PrintedTotal { get; set; }
    public int? Total { get; set; }
    public string LogoUrl { get; set; } = "";
    public string SymbolUrl { get; set; } = "";
}

public sealed class CatalogSetCardDto
{
    public string ExternalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string SetId { get; set; } = "";
    public string SetName { get; set; } = "";
    public string Series { get; set; } = "";
    public string Number { get; set; } = "";
    public string Rarity { get; set; } = "";
    public string Supertype { get; set; } = "";
    public string Subtypes { get; set; } = "";
    public decimal? MarketPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public decimal? MidPrice { get; set; }
    public decimal? HighPrice { get; set; }
    public DateTime? PriceUpdatedAtUtc { get; set; }
    public string TcgPlayerUrl { get; set; } = "";
    public string RemoteImageUrl { get; set; } = "";
    public string LocalImagePath { get; set; } = "";
}

public sealed class CatalogSetImportResultDto
{
    public string SetId { get; set; } = "";
    public string SetName { get; set; } = "";
    public int TotalCardsFound { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int ImageWarmCount { get; set; }
    public int ImageWarmFailedCount { get; set; }
    public DateTime ImportedAtUtc { get; set; }
}

public sealed record CatalogImageResult(byte[] Bytes, string ContentType);