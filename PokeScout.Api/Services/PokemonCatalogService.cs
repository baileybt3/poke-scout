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

        var url = $"images/{Uri.EscapeDataString(id)}?size={Uri.EscapeDataString(size)}";

        var response = await _http.GetAsync(url, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = TryDecodeUtf8(bytes);
            throw new Exception($"PokeWallet image failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{errorText}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        return new CatalogImageResult(bytes, contentType);
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
        // Prefer tcgplayer market price
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

        // Fall back to cardmarket avg/low/trend
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