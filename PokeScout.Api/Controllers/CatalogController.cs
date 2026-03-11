using Microsoft.AspNetCore.Mvc;
using PokeScout.Api.Dtos;
using PokeScout.Api.Services;

namespace PokeScout.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IPokemonCatalogService _catalogService;

    public CatalogController(IPokemonCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<CatalogSearchResultDto>>> Search(
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("name is required.");

        var results = await _catalogService.SearchByNameAsync(name, cancellationToken);
        return Ok(results);
    }

    [HttpGet("image/{id}")]
    public async Task<IActionResult> Image(
        string id,
        [FromQuery] string size = "high",
        CancellationToken cancellationToken = default)
    {
        var image = await _catalogService.GetImageAsync(id, size, cancellationToken);
        return File(image.Bytes, image.ContentType);
    }
}