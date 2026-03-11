using PokeScout.Api.Dtos;

namespace PokeScout.Api.Services;

public interface IPokemonCatalogService
{
    Task<List<CatalogSearchResultDto>> SearchByNameAsync(string name, CancellationToken cancellationToken = default);
}