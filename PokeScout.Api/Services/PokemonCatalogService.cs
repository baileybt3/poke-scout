using System.Net;
using System.Text;
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

        var url = $"search?q={Uri.EscapeDataString(name)}";

        var response = await _http.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"PokeWallet search failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        using var doc = JsonDocument.Parse(body);
        var cards = ExtractCardArray(doc.RootElement);

        var results = new List<CatalogSearchResultDto>();

        foreach (var card in cards)
        {
            var id = GetString(card, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var cardInfo = GetObject(card, "card_info");

            var cardName =
                GetString(cardInfo, "name") ??
                GetString(card, "name") ??
                "";

            var setName =
                GetString(cardInfo, "set_name") ??
                GetNestedString(card, "set", "name") ??
                GetString(card, "set_name") ??
                "";

            var rarity =
                GetString(cardInfo, "rarity") ??
                GetString(card, "rarity") ??
                "";

            var marketPrice = TryGetMarketPrice(card);

            results.Add(new CatalogSearchResultDto
            {
                ExternalId = id,
                Name = cardName,
                ImageUrl = $"/api/catalog/image/{Uri.EscapeDataString(id)}?size=high",
                SetName = setName,
                Series = "",
                Rarity = rarity,
                MarketPrice = marketPrice,
                TcgPlayerUrl = GetNestedString(card, "tcgplayer", "url") ?? ""
            });
        }

        return results;
    }

    public async Task<CatalogImageResult> GetImageAsync(string id, string size = "high", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Image id is required.", nameof(id));

        var normalizedSize = string.IsNullOrWhiteSpace(size)
            ? "high"
            : size.Trim().ToLowerInvariant();

        var primaryResult = await TryGetImageAsync(id, normalizedSize, cancellationToken);
        if (primaryResult is not null)
            return primaryResult;

        if (normalizedSize == "high")
        {
            var lowResult = await TryGetImageAsync(id, "low", cancellationToken);
            if (lowResult is not null)
                return lowResult;
        }

        return CreatePlaceholderImageResult();
    }

    private async Task<CatalogImageResult?> TryGetImageAsync(string id, string size, CancellationToken cancellationToken)
    {
        var url = $"images/{Uri.EscapeDataString(id)}?size={Uri.EscapeDataString(size)}";

        var response = await _http.GetAsync(url, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return new CatalogImageResult(bytes, contentType);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var errorText = TryDecodeUtf8(bytes);
        throw new Exception($"PokeWallet image failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{errorText}");
    }

    private static CatalogImageResult CreatePlaceholderImageResult()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="360" height="500" viewBox="0 0 360 500">
              <rect width="360" height="500" rx="24" fill="#0f172a"/>
              <rect x="18" y="18" width="324" height="464" rx="18" fill="#111827" stroke="#334155" stroke-width="2"/>
              <circle cx="180" cy="180" r="56" fill="#1e293b"/>
              <path d="M124 180h112" stroke="#64748b" stroke-width="14" stroke-linecap="round"/>
              <circle cx="180" cy="180" r="18" fill="#94a3b8"/>
              <text x="180" y="300" text-anchor="middle" fill="#e2e8f0" font-size="24" font-family="Arial, Helvetica, sans-serif" font-weight="700">
                Image unavailable
              </text>
              <text x="180" y="334" text-anchor="middle" fill="#94a3b8" font-size="16" font-family="Arial, Helvetica, sans-serif">
                PokeWallet did not return this card image
              </text>
            </svg>
            """;

        return new CatalogImageResult(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }

    private static List<JsonElement> ExtractCardArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToList();

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "data", "results", "cards", "items" })
            {
                if (root.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    return arr.EnumerateArray().ToList();
            }
        }

        return [];
    }

    private static JsonElement? GetObject(JsonElement obj, string name)
    {
        if (obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        return null;
    }

    private static string? GetString(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static string? GetString(JsonElement? obj, params string[] names)
    {
        if (obj is null)
            return null;

        return GetString(obj.Value, names);
    }

    private static string? GetNestedString(JsonElement obj, string parent, params string[] childNames)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        if (!obj.TryGetProperty(parent, out var parentValue) || parentValue.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var child in childNames)
        {
            if (parentValue.TryGetProperty(child, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static decimal? TryGetMarketPrice(JsonElement card)
    {
        if (card.TryGetProperty("tcgplayer", out var tcgplayer) &&
            tcgplayer.ValueKind == JsonValueKind.Object &&
            tcgplayer.TryGetProperty("prices", out var tcgPrices) &&
            tcgPrices.ValueKind == JsonValueKind.Array)
        {
            foreach (var price in tcgPrices.EnumerateArray())
            {
                var market = GetDecimal(price, "market_price", "market", "mid_price", "low_price");
                if (market is not null)
                    return market;
            }
        }

        if (card.TryGetProperty("cardmarket", out var cardmarket) &&
            cardmarket.ValueKind == JsonValueKind.Object &&
            cardmarket.TryGetProperty("prices", out var cmPrices) &&
            cmPrices.ValueKind == JsonValueKind.Array)
        {
            foreach (var price in cmPrices.EnumerateArray())
            {
                var market = GetDecimal(price, "avg", "trend", "low");
                if (market is not null)
                    return market;
            }
        }

        return null;
    }

    private static decimal? GetDecimal(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var num))
                return num;

            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return null;
    }

    private static string TryDecodeUtf8(byte[] bytes)
    {
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "[binary response]";
        }
    }
}