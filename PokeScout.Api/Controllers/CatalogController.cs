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

    [HttpGet("sets")]
    public async Task<IActionResult> GetSets(CancellationToken cancellationToken)
    {
        var sets = await _catalogService.GetSetsAsync(cancellationToken);
        return Ok(sets);
    }

    [HttpGet("sets/{setId}/cards")]
    public async Task<IActionResult> GetCardsBySet(
        string setId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(setId))
            return BadRequest("setId is required.");

        var cards = await _catalogService.GetCardsBySetAsync(setId, cancellationToken);
        return Ok(cards);
    }

    [HttpGet("sets/{setId}/image")]
    public async Task<IActionResult> GetSetImage(
        string setId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setId))
            return BadRequest("setId is required.");

        var image = await _catalogService.GetSetImageAsync(setId, cancellationToken);
        return File(image.Bytes, image.ContentType);
    }

    [HttpGet("image/{id}")]
    public async Task<IActionResult> Image(
        string id,
        [FromQuery] string size = "high",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("id is required.");

        var image = await _catalogService.GetImageAsync(id, size, cancellationToken);
        return File(image.Bytes, image.ContentType);
    }
}