using Microsoft.AspNetCore.Mvc;
using PokeScout.Api.Dtos;
using PokeScout.Api.Services;

namespace PokeScout.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IPokemonCatalogService _catalog;

    public CatalogController(IPokemonCatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<CatalogSearchResultDto>>> Search(
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("name is required.");

        var results = await _catalog.SearchByNameAsync(name, cancellationToken);
        return Ok(results);
    }
}