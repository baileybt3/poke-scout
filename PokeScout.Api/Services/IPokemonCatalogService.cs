using PokeScout.Api.Dtos;

namespace PokeScout.Api.Services;

public interface IPokemonCatalogService
{
    Task<List<CatalogSearchResultDto>> SearchByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<CatalogImageResult> GetImageAsync(string id, string size = "high", CancellationToken cancellationToken = default);
}

public sealed record CatalogImageResult(byte[] Bytes, string ContentType);