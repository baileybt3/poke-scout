using System.Text.Json;
using PokeScout.Api.Dtos;

namespace PokeScout.Api.Services;

public sealed class PokemonCatalogService : IPokemonCatalogService
{
    private readonly HttpClient _http;

    public PokemonCatalogService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CatalogSearchResultDto>> SearchByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return [];

        var searchUrl = $"cards?name={Uri.EscapeDataString(name)}";

        var searchResponse = await _http.GetAsync(searchUrl, cancellationToken);
        var searchBody = await searchResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!searchResponse.IsSuccessStatusCode)
        {
            throw new Exception($"TCGdex search failed: {(int)searchResponse.StatusCode} {searchResponse.ReasonPhrase}\n{searchBody}");
        }

        var searchResults = JsonSerializer.Deserialize<List<TcgDexCardBrief>>(
            searchBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

        if (searchResults.Count == 0)
            return [];

        var topResults = searchResults.Take(12).ToList();
        var finalResults = new List<CatalogSearchResultDto>();

        foreach (var brief in topResults)
        {
            if (string.IsNullOrWhiteSpace(brief.Id))
                continue;

            var detail = await GetCardDetailsAsync(brief.Id, cancellationToken);

            finalResults.Add(new CatalogSearchResultDto
            {
                ExternalId = detail?.Id ?? brief.Id ?? "",
                Name = detail?.Name ?? brief.Name ?? "",
                ImageUrl = NormalizeImageUrl(detail?.Image ?? brief.Image),
                SetName = detail?.Set?.Name ?? "",
                Series = "",
                Rarity = detail?.Rarity ?? "",
                MarketPrice = null,
                TcgPlayerUrl = ""
            });
        }

        return finalResults;
    }

    private async Task<TcgDexCard?> GetCardDetailsAsync(string id, CancellationToken cancellationToken)
    {
        var detailResponse = await _http.GetAsync($"cards/{Uri.EscapeDataString(id)}", cancellationToken);
        var detailBody = await detailResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!detailResponse.IsSuccessStatusCode)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TcgDexCard>(
            detailBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    private static string NormalizeImageUrl(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
            return "";

        if (image.EndsWith("/low.webp", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith("/high.webp", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith("/low.png", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith("/high.png", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith("/low.jpg", StringComparison.OrdinalIgnoreCase) ||
            image.EndsWith("/high.jpg", StringComparison.OrdinalIgnoreCase))
        {
            return image;
        }

        return $"{image}/low.webp";
    }

    private sealed class TcgDexCardBrief
    {
        public string? Id { get; set; }
        public string? LocalId { get; set; }
        public string? Name { get; set; }
        public string? Image { get; set; }
    }

    private sealed class TcgDexCard
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Image { get; set; }
        public string? Rarity { get; set; }
        public TcgDexSetBrief? Set { get; set; }
    }

    private sealed class TcgDexSetBrief
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}