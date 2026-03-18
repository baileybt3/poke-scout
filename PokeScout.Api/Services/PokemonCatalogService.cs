using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PokeScout.Api.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace PokeScout.Api.Services;


public sealed class PokemonCatalogService : IPokemonCatalogService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private const int SetCardsPageSize = 200;

    public PokemonCatalogService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }
    
    public async Task<List<CatalogSearchResultDto>> SearchByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return [];

        using var doc = await GetJsonDocumentAsync(
            $"search?q={Uri.EscapeDataString(name.Trim())}",
            cancellationToken);

        var cards = ExtractSearchCards(doc.RootElement);
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

            var setCode =
                GetString(cardInfo, "set_code") ??
                GetNestedString(card, "set", "set_code", "code") ??
                "";

            var rarity =
                GetString(cardInfo, "rarity") ??
                GetString(card, "rarity") ??
                "";

            var marketPrice = TryGetMarketPrice(card);
            var tcgPlayerUrl =
                GetNestedString(card, "tcgplayer", "url") ?? "";

            results.Add(new CatalogSearchResultDto
            {
                ExternalId = id,
                Name = cardName,
                ImageUrl = BuildImageProxyUrl(id),
                SetName = setName,
                Series = setCode,
                Rarity = rarity,
                MarketPrice = marketPrice,
                TcgPlayerUrl = tcgPlayerUrl
            });
        }

        return results;
    }

    public async Task<List<CatalogSetListItemDto>> GetSetsAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonDocumentAsync("sets", cancellationToken);

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<CatalogSetListItemDto>();

        foreach (var set in data.EnumerateArray())
        {
            var setId = GetString(set, "set_id") ?? "";
            var setName = GetString(set, "name") ?? "";

            if (string.IsNullOrWhiteSpace(setId) || string.IsNullOrWhiteSpace(setName))
                continue;

            var setCode = GetString(set, "set_code") ?? "";
            var releaseDateRaw = GetString(set, "release_date") ?? "";
            var cardCount = GetInt(set, "card_count");

            results.Add(new CatalogSetListItemDto
            {
                Id = setId,
                Name = setName,
                Series = setCode,
                ReleaseDate = NormalizeReleaseDate(releaseDateRaw),
                PrintedTotal = cardCount,
                Total = cardCount,
                LogoUrl = $"/api/catalog/sets/{Uri.EscapeDataString(setId)}/image",
                SymbolUrl = ""
            });
        }

        return results
            .OrderBy(x => ParseReleaseDateForSort(x.ReleaseDate) ?? DateTime.MaxValue)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<CatalogSetCardDto>> GetCardsBySetAsync(
        string setId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setId))
            return [];

        var trimmedSetId = setId.Trim();
        var escapedSetId = Uri.EscapeDataString(trimmedSetId);

        var results = new List<CatalogSetCardDto>();
        var page = 1;
        int? totalPages = null;

        while (true)
        {
            using var doc = await GetJsonDocumentAsync(
                $"sets/{escapedSetId}?page={page}&limit={SetCardsPageSize}",
                cancellationToken);

            var root = doc.RootElement;

            if (root.TryGetProperty("disambiguation", out var disambiguationElement) &&
                disambiguationElement.ValueKind == JsonValueKind.True)
            {
                throw new Exception(
                    $"PokeWallet returned an ambiguous set match for '{trimmedSetId}'. Use the numeric set_id from /sets instead.");
            }

            var setObject = GetObject(root, "set");
            var resolvedSetId =
                GetString(setObject, "set_id") ??
                trimmedSetId;

            var resolvedSetName =
                GetString(setObject, "name") ??
                "";

            var resolvedSetCode =
                GetString(setObject, "set_code") ??
                "";

            if (root.TryGetProperty("pagination", out var pagination) &&
                pagination.ValueKind == JsonValueKind.Object)
            {
                totalPages = GetInt(pagination, "total_pages");
            }

            if (!root.TryGetProperty("cards", out var cardsArray) || cardsArray.ValueKind != JsonValueKind.Array)
                break;

            foreach (var card in cardsArray.EnumerateArray())
            {
                var id = GetString(card, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var cardInfo = GetObject(card, "card_info");

                var cardName =
                    GetString(cardInfo, "name") ??
                    GetString(card, "name") ??
                    "";

                var number =
                    GetString(cardInfo, "card_number", "number") ??
                    GetString(card, "card_number", "number") ??
                    "";

                var rarity =
                    GetString(cardInfo, "rarity") ??
                    GetString(card, "rarity") ??
                    "";

                var marketPrice = TryGetMarketPrice(card);

                var tcgPlayerUrl =
                    GetNestedString(card, "tcgplayer", "url") ??
                    GetNestedString(card, "cardmarket", "product_url") ??
                    "";

                results.Add(new CatalogSetCardDto
                {
                    ExternalId = id,
                    Name = cardName,
                    ImageUrl = BuildImageProxyUrl(id),
                    SetId = resolvedSetId,
                    SetName = resolvedSetName,
                    Series = resolvedSetCode,
                    Number = NormalizeCardNumber(number),
                    Rarity = rarity,
                    MarketPrice = marketPrice,
                    TcgPlayerUrl = tcgPlayerUrl
                });
            }

            if (cardsArray.GetArrayLength() < SetCardsPageSize)
                break;

            if (totalPages.HasValue && page >= totalPages.Value)
                break;

            page++;
        }

        return results
            .OrderBy(x => GetCardNumberSortBucket(x.Number))
            .ThenBy(x => GetCardNumberNumericPart(x.Number))
            .ThenBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CatalogImageResult> GetSetImageAsync(
        string setId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setId))
            throw new ArgumentException("Set id is required.", nameof(setId));

        var trimmedSetId = setId.Trim();
        var response = await _http.GetAsync(
            $"sets/{Uri.EscapeDataString(trimmedSetId)}/image",
            cancellationToken);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            return new CatalogImageResult(bytes, contentType);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return CreateSetPlaceholderImageResult();

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var errorText = TryDecodeUtf8(bytes);
            throw new Exception(
                $"PokeWallet set image request was ambiguous for '{trimmedSetId}'. Use numeric set_id from /sets.\n{errorText}");
        }

        var responseText = TryDecodeUtf8(bytes);
        throw new Exception(
            $"PokeWallet set image failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{responseText}");
    }

    public async Task<CatalogImageResult> GetImageAsync(
    string id,
    string size = "high",
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Image id is required.", nameof(id));

        var normalizedSize = string.IsNullOrWhiteSpace(size)
            ? "high"
            : size.Trim().ToLowerInvariant();

        var cacheKey = $"catalog:image:{normalizedSize}:{id.Trim()}";

        if (_cache.TryGetValue(cacheKey, out CatalogImageResult? cached) && cached is not null)
            return cached;

        CatalogImageResult result;

        try
        {
            var primaryResult = await TryGetImageAsync(id, normalizedSize, cancellationToken);
            if (primaryResult is not null)
            {
                result = primaryResult;
            }
            else if (normalizedSize == "high")
            {
                var lowResult = await TryGetImageAsync(id, "low", cancellationToken);
                result = lowResult ?? CreatePlaceholderImageResult();
            }
            else
            {
                result = CreatePlaceholderImageResult();
            }
        }
        catch
        {
            result = CreatePlaceholderImageResult();
        }

        _cache.Set(
            cacheKey,
            result,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });

        return result;
    }

    private static string BuildImageProxyUrl(string id, string size = "high")
    {
        return $"/api/catalog/image?id={Uri.EscapeDataString(id)}&size={Uri.EscapeDataString(size)}";
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync(relativeUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"PokeWallet request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        return JsonDocument.Parse(body);
    }

    private async Task<CatalogImageResult?> TryGetImageAsync(
    string id,
    string size,
    CancellationToken cancellationToken)
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
            return null;

        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
        {
            return CreatePlaceholderImageResult();
        }

        return CreatePlaceholderImageResult();
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

    private static CatalogImageResult CreateSetPlaceholderImageResult()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="600" height="240" viewBox="0 0 600 240">
              <rect width="600" height="240" rx="24" fill="#0f172a"/>
              <rect x="16" y="16" width="568" height="208" rx="18" fill="#111827" stroke="#334155" stroke-width="2"/>
              <text x="300" y="110" text-anchor="middle" fill="#e2e8f0" font-size="30" font-family="Arial, Helvetica, sans-serif" font-weight="700">
                Set image unavailable
              </text>
              <text x="300" y="145" text-anchor="middle" fill="#94a3b8" font-size="18" font-family="Arial, Helvetica, sans-serif">
                PokeWallet did not return a logo for this set
              </text>
            </svg>
            """;

        return new CatalogImageResult(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }

    private static List<JsonElement> ExtractSearchCards(JsonElement root)
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

            if (root.TryGetProperty("data", out var dataObj) && dataObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "cards", "items", "results" })
                {
                    if (dataObj.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                        return arr.EnumerateArray().ToList();
                }
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

    private static int? GetInt(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
                return intValue;

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
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

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string NormalizeReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var cleaned = Regex.Replace(value, @"(\d+)(st|nd|rd|th)", "$1", RegexOptions.IgnoreCase);

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("yyyy-MM-dd");

        return value.Trim();
    }

    private static DateTime? ParseReleaseDateForSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var formats = new[]
        {
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "M/d/yyyy",
            "yyyy/MM/dd",
            "yyyy-MM",
            "yyyy"
        };

        if (DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var exact))
        {
            return exact;
        }

        var cleaned = Regex.Replace(value, @"(\d+)(st|nd|rd|th)", "$1", RegexOptions.IgnoreCase);

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        return null;
    }

    private static string NormalizeCardNumber(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }

    private static int GetCardNumberSortBucket(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return 2;

        return int.TryParse(GetLeadingDigits(number), out _) ? 0 : 1;
    }

    private static int GetCardNumberNumericPart(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return int.MaxValue;

        var digits = GetLeadingDigits(number);
        return int.TryParse(digits, out var parsed) ? parsed : int.MaxValue;
    }

    private static string GetLeadingDigits(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Contains('/'))
            trimmed = trimmed.Split('/')[0];

        var chars = trimmed.TakeWhile(char.IsDigit).ToArray();
        return new string(chars);
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