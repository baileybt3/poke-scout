using Microsoft.AspNetCore.Mvc;
using PokeScout.Api.Dtos;
using PokeScout.Api.Services;

namespace PokeScout.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IPokemonCatalogService _catalogService;
    private readonly IWebHostEnvironment _environment;

    public CatalogController(
        IPokemonCatalogService catalogService,
        IWebHostEnvironment environment)
    {
        _catalogService = catalogService;
        _environment = environment;
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

    [HttpGet("image")]
    public async Task<IActionResult> Image(
        [FromQuery] string id,
        [FromQuery] string size = "high",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("id is required.");

        var image = await _catalogService.GetImageAsync(id, size, cancellationToken);
        return File(image.Bytes, image.ContentType);
    }

    [HttpPost("sets/{setId}/warm-cache")]
    public async Task<IActionResult> WarmSetCache(
        string setId,
        [FromQuery] string size = "low",
        [FromQuery] int? maxImages = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setId))
            return BadRequest("setId is required.");

        var cards = await _catalogService.GetCardsBySetAsync(setId, cancellationToken);

        var warmed = 0;
        var failed = 0;

        var cardsToWarm = maxImages.HasValue
            ? cards.Take(maxImages.Value).ToList()
            : cards;

        foreach (var card in cardsToWarm)
        {
            if (string.IsNullOrWhiteSpace(card.ExternalId))
                continue;

            try
            {
                await _catalogService.GetImageAsync(card.ExternalId, size, cancellationToken);
                warmed++;
            }
            catch
            {
                failed++;
            }
        }

        return Ok(new
        {
            setId,
            size,
            totalCardsReturned = cards.Count,
            attempted = cardsToWarm.Count,
            warmed,
            failed
        });
    }

    [HttpDelete("cache")]
    public IActionResult ClearCache()
    {
        var cacheRoot = Path.Combine(_environment.ContentRootPath, "Storage", "CatalogCache");

        if (Directory.Exists(cacheRoot))
        {
            Directory.Delete(cacheRoot, recursive: true);
        }

        Directory.CreateDirectory(Path.Combine(cacheRoot, "Json"));
        Directory.CreateDirectory(Path.Combine(cacheRoot, "Images"));

        return Ok(new
        {
            message = "Catalog cache cleared.",
            path = cacheRoot
        });
    }

    [HttpPost("sets/{setId}/import")]
    public async Task<IActionResult> ImportSetToCatalog(
    string setId,
    [FromQuery] bool warmImages = false,
    [FromQuery] int? maxImagesToWarm = null,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setId))
            return BadRequest("setId is required.");

        var result = await _catalogService.ImportSetToCatalogAsync(
            setId,
            warmImages,
            maxImagesToWarm,
            cancellationToken);

        return Ok(result);
    }
}