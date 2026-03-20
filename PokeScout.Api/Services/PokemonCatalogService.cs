using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PokeScout.Api.Data;
using PokeScout.Api.Dtos;
using PokeScout.Api.Models;

namespace PokeScout.Api.Services;

public sealed class PokemonCatalogService : IPokemonCatalogService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _environment;
    private readonly PokeScoutDbContext _db;

    private readonly string _cacheRoot;
    private readonly string _jsonCacheRoot;
    private readonly string _imageCacheRoot;

    private const int SetCardsPageSize = 200;
    private const int MinimumRemainingHourlyRequestsForLiveImages = 10;

    private static readonly TimeSpan SetsListCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan SetCardsCacheDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(15);

    private static readonly object RateLimitLock = new();
    private static int? _remainingHourlyRequests;
    private static int? _remainingDailyRequests;
    private static DateTimeOffset? _rateLimitCapturedAtUtc;

    public PokemonCatalogService(
        HttpClient http,
        IMemoryCache cache,
        IWebHostEnvironment environment,
        PokeScoutDbContext db)
    {
        _http = http;
        _cache = cache;
        _environment = environment;
        _db = db;

        _cacheRoot = Path.Combine(_environment.ContentRootPath, "Storage", "CatalogCache");
        _jsonCacheRoot = Path.Combine(_cacheRoot, "Json");
        _imageCacheRoot = Path.Combine(_cacheRoot, "Images");

        Directory.CreateDirectory(_cacheRoot);
        Directory.CreateDirectory(_jsonCacheRoot);
        Directory.CreateDirectory(_imageCacheRoot);
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
                ImageUrl = BuildImageProxyUrl(id, "low"),
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

        var localCards = await GetCardsBySetFromDatabaseAsync(trimmedSetId, cancellationToken);
        if (localCards.Count > 0)
            return localCards;

        return await GetCardsBySetFromApiAsync(trimmedSetId, cancellationToken);
    }

    public async Task<CatalogSetImportResultDto> ImportSetToCatalogAsync(
        string setId,
        bool warmImages = false,
        int? maxImagesToWarm = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setId))
            throw new ArgumentException("Set id is required.", nameof(setId));

        var cards = await GetCardsBySetFromApiAsync(setId, cancellationToken);
        var importedAtUtc = DateTime.UtcNow;

        if (cards.Count == 0)
        {
            return new CatalogSetImportResultDto
            {
                SetId = setId,
                SetName = "",
                TotalCardsFound = 0,
                InsertedCount = 0,
                UpdatedCount = 0,
                ImageWarmCount = 0,
                ImageWarmFailedCount = 0,
                ImportedAtUtc = importedAtUtc
            };
        }

        var externalIds = cards
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
            .Select(x => x.ExternalId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _db.CatalogCards
            .Where(x => externalIds.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var insertedCount = 0;
        var updatedCount = 0;
        var imageWarmCount = 0;
        var imageWarmFailedCount = 0;
        var warmLimit = maxImagesToWarm ?? int.MaxValue;
        var warmAttempts = 0;

        foreach (var dto in cards)
        {
            if (string.IsNullOrWhiteSpace(dto.ExternalId))
                continue;

            if (!existing.TryGetValue(dto.ExternalId, out var entity))
            {
                entity = new CatalogCard
                {
                    Id = Guid.NewGuid(),
                    CreatedAtUtc = importedAtUtc
                };

                _db.CatalogCards.Add(entity);
                existing[dto.ExternalId] = entity;
                insertedCount++;
            }
            else
            {
                updatedCount++;
            }

            ApplyCatalogCardValues(entity, dto, importedAtUtc);

            if (warmImages && warmAttempts < warmLimit)
            {
                warmAttempts++;

                try
                {
                    var image = await GetImageAsync(dto.ExternalId, "low", cancellationToken);

                    if (ContentTypeLooksLikeImage(image.ContentType) &&
                        !string.Equals(image.ContentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
                    {
                        entity.LocalImagePath = GetExistingLocalImagePath(dto.ExternalId, "low");
                        entity.ImageLastSyncedAtUtc = importedAtUtc;
                        imageWarmCount++;
                    }
                    else
                    {
                        imageWarmFailedCount++;
                    }
                }
                catch
                {
                    imageWarmFailedCount++;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new CatalogSetImportResultDto
        {
            SetId = cards.FirstOrDefault()?.SetId ?? setId,
            SetName = cards.FirstOrDefault()?.SetName ?? "",
            TotalCardsFound = cards.Count,
            InsertedCount = insertedCount,
            UpdatedCount = updatedCount,
            ImageWarmCount = imageWarmCount,
            ImageWarmFailedCount = imageWarmFailedCount,
            ImportedAtUtc = importedAtUtc
        };
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

        UpdateRateLimitState(response);

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
        string size = "low",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Image id is required.", nameof(id));

        var normalizedSize = string.IsNullOrWhiteSpace(size)
            ? "low"
            : size.Trim().ToLowerInvariant();

        var cacheKey = $"catalog:image:{normalizedSize}:{id.Trim()}";

        if (_cache.TryGetValue(cacheKey, out CatalogImageResult? cached) && cached is not null)
            return cached;

        if (TryReadCachedImage(id, normalizedSize, out var diskCached) && diskCached is not null)
        {
            _cache.Set(
                cacheKey,
                diskCached,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });

            return diskCached;
        }

        CatalogImageResult result;

        try
        {
            if (!ShouldAllowLiveImageFetch())
            {
                result = CreatePlaceholderImageResult();
            }
            else
            {
                var primaryResult = await TryGetImageAsync(id, normalizedSize, cancellationToken);
                if (primaryResult is not null)
                {
                    result = primaryResult;
                    TryWriteCachedImage(id, normalizedSize, result);
                }
                else if (normalizedSize == "high")
                {
                    var lowResult = await TryGetImageAsync(id, "low", cancellationToken);
                    if (lowResult is not null)
                    {
                        result = lowResult;
                        TryWriteCachedImage(id, "low", result);
                    }
                    else
                    {
                        result = CreatePlaceholderImageResult();
                    }
                }
                else
                {
                    result = CreatePlaceholderImageResult();
                }
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

    private async Task<List<CatalogSetCardDto>> GetCardsBySetFromDatabaseAsync(
    string setId,
    CancellationToken cancellationToken)
    {
        var cards = await _db.CatalogCards
            .AsNoTracking()
            .Where(x => x.SetApiId == setId || x.SetCode == setId)
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (cards.Count == 0)
            return [];

        return cards
            .Select(MapCatalogCardToDto)
            .OrderBy(x => GetCardNumberSortBucket(x.Number))
            .ThenBy(x => GetCardNumberNumericPart(x.Number))
            .ThenBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<CatalogSetCardDto>> GetCardsBySetFromApiAsync(
        string setId,
        CancellationToken cancellationToken)
    {
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

                var supertype =
                    GetString(cardInfo, "supertype", "type") ??
                    GetString(card, "supertype", "type") ??
                    "";

                var subtypes =
                    GetJoinedStrings(cardInfo, "subtypes") ??
                    GetJoinedStrings(card, "subtypes") ??
                    "";

                var tcgPlayerUrl =
                    GetNestedString(card, "tcgplayer", "url") ??
                    GetNestedString(card, "cardmarket", "product_url") ??
                    "";

                var priceSnapshot = ExtractPriceSnapshot(card);

                results.Add(new CatalogSetCardDto
                {
                    ExternalId = id,
                    Name = cardName,
                    ImageUrl = BuildImageProxyUrl(id, "low"),
                    SetId = resolvedSetId,
                    SetName = resolvedSetName,
                    Series = resolvedSetCode,
                    Number = NormalizeCardNumber(number),
                    Rarity = rarity,
                    Supertype = supertype,
                    Subtypes = subtypes,
                    MarketPrice = priceSnapshot.MarketPrice,
                    LowPrice = priceSnapshot.LowPrice,
                    MidPrice = priceSnapshot.MidPrice,
                    HighPrice = priceSnapshot.HighPrice,
                    PriceUpdatedAtUtc = priceSnapshot.UpdatedAtUtc,
                    TcgPlayerUrl = tcgPlayerUrl,
                    RemoteImageUrl = BuildImageProxyUrl(id, "low"),
                    LocalImagePath = GetExistingLocalImagePath(id, "low")
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

    private CatalogSetCardDto MapCatalogCardToDto(CatalogCard card)
    {
        var localPath = GetExistingLocalImagePath(card.ExternalId, "low");

        return new CatalogSetCardDto
        {
            ExternalId = card.ExternalId,
            Name = card.Name,
            ImageUrl = BuildImageProxyUrl(card.ExternalId, "low"),
            SetId = card.SetApiId,
            SetName = card.SetName,
            Series = card.SetCode,
            Number = NormalizeCardNumber(card.Number),
            Rarity = card.Rarity,
            Supertype = card.Supertype,
            Subtypes = card.Subtypes,
            MarketPrice = card.MarketPrice,
            LowPrice = card.LowPrice,
            MidPrice = card.MidPrice,
            HighPrice = card.HighPrice,
            PriceUpdatedAtUtc = card.PriceUpdatedAtUtc,
            TcgPlayerUrl = card.TcgPlayerUrl,
            RemoteImageUrl = string.IsNullOrWhiteSpace(card.RemoteImageUrl)
                ? BuildImageProxyUrl(card.ExternalId, "low")
                : card.RemoteImageUrl,
            LocalImagePath = localPath
        };
    }

    private void ApplyCatalogCardValues(CatalogCard entity, CatalogSetCardDto dto, DateTime importedAtUtc)
    {
        entity.ExternalId = dto.ExternalId?.Trim() ?? "";
        entity.Name = dto.Name?.Trim() ?? "";
        entity.SetName = dto.SetName?.Trim() ?? "";
        entity.SetApiId = dto.SetId?.Trim() ?? "";
        entity.SetCode = dto.Series?.Trim() ?? "";
        entity.Number = dto.Number?.Trim() ?? "";
        entity.Rarity = dto.Rarity?.Trim() ?? "";
        entity.Supertype = dto.Supertype?.Trim() ?? "";
        entity.Subtypes = dto.Subtypes?.Trim() ?? "";

        entity.RemoteImageUrl = string.IsNullOrWhiteSpace(dto.RemoteImageUrl)
            ? BuildImageProxyUrl(entity.ExternalId, "low")
            : dto.RemoteImageUrl;

        var existingLocalPath = GetExistingLocalImagePath(entity.ExternalId, "low");
        if (!string.IsNullOrWhiteSpace(existingLocalPath))
        {
            entity.LocalImagePath = existingLocalPath;
            entity.ImageLastSyncedAtUtc ??= importedAtUtc;
        }

        entity.TcgPlayerUrl = dto.TcgPlayerUrl?.Trim() ?? "";
        entity.MarketPrice = dto.MarketPrice;
        entity.LowPrice = dto.LowPrice;
        entity.MidPrice = dto.MidPrice;
        entity.HighPrice = dto.HighPrice;
        entity.PriceUpdatedAtUtc = dto.PriceUpdatedAtUtc ?? entity.PriceUpdatedAtUtc;
        entity.CatalogLastSyncedAtUtc = importedAtUtc;
        entity.UpdatedAtUtc = importedAtUtc;
    }

    private string GetExistingLocalImagePath(string id, string size)
    {
        var folder = GetImageCacheFolder(id, size);
        var bytesPath = Path.Combine(folder, "image.bin");
        var contentTypePath = Path.Combine(folder, "content-type.txt");

        if (!File.Exists(bytesPath) || !File.Exists(contentTypePath))
            return "";

        return BuildImageProxyUrl(id, size);
    }

    private static string BuildImageProxyUrl(string id, string size = "low")
    {
        return $"/api/catalog/image?id={Uri.EscapeDataString(id)}&size={Uri.EscapeDataString(size)}";
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        if (TryReadCachedJson(relativeUrl, out var cachedBody) && !string.IsNullOrWhiteSpace(cachedBody))
            return JsonDocument.Parse(cachedBody);

        var response = await _http.GetAsync(relativeUrl, cancellationToken);
        UpdateRateLimitState(response);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"PokeWallet request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        TryWriteCachedJson(relativeUrl, body);

        return JsonDocument.Parse(body);
    }

    private async Task<CatalogImageResult?> TryGetImageAsync(
        string id,
        string size,
        CancellationToken cancellationToken)
    {
        var url = $"images/{Uri.EscapeDataString(id)}?size={Uri.EscapeDataString(size)}";

        var response = await _http.GetAsync(url, cancellationToken);
        UpdateRateLimitState(response);

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
            return null;
        }

        return null;
    }

    private bool TryReadCachedJson(string relativeUrl, out string? body)
    {
        body = null;

        var lifetime = GetJsonCacheDuration(relativeUrl);
        if (lifetime is null)
            return false;

        var path = GetJsonCachePath(relativeUrl);
        if (!File.Exists(path))
            return false;

        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
        if (age > lifetime.Value)
            return false;

        body = File.ReadAllText(path);
        return true;
    }

    private void TryWriteCachedJson(string relativeUrl, string body)
    {
        var lifetime = GetJsonCacheDuration(relativeUrl);
        if (lifetime is null)
            return;

        var path = GetJsonCachePath(relativeUrl);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
    }

    private TimeSpan? GetJsonCacheDuration(string relativeUrl)
    {
        if (relativeUrl.Equals("sets", StringComparison.OrdinalIgnoreCase))
            return SetsListCacheDuration;

        if (relativeUrl.StartsWith("sets/", StringComparison.OrdinalIgnoreCase))
            return SetCardsCacheDuration;

        if (relativeUrl.StartsWith("search?", StringComparison.OrdinalIgnoreCase))
            return SearchCacheDuration;

        return null;
    }

    private string GetJsonCachePath(string relativeUrl)
    {
        if (relativeUrl.Equals("sets", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(_jsonCacheRoot, "Sets", "sets.json");
        }

        if (relativeUrl.StartsWith("sets/", StringComparison.OrdinalIgnoreCase))
        {
            var withoutPrefix = relativeUrl["sets/".Length..];
            var pathPart = withoutPrefix;
            var queryPart = "";

            var queryIndex = withoutPrefix.IndexOf('?');
            if (queryIndex >= 0)
            {
                pathPart = withoutPrefix[..queryIndex];
                queryPart = withoutPrefix[(queryIndex + 1)..];
            }

            var setId = SanitizeFileSegment(pathPart);

            var page = GetQueryStringValue(queryPart, "page") ?? "1";
            var limit = GetQueryStringValue(queryPart, "limit") ?? SetCardsPageSize.ToString(CultureInfo.InvariantCulture);

            return Path.Combine(
                _jsonCacheRoot,
                "SetCards",
                setId,
                $"page-{SanitizeFileSegment(page)}-limit-{SanitizeFileSegment(limit)}.json");
        }

        if (relativeUrl.StartsWith("search?", StringComparison.OrdinalIgnoreCase))
        {
            var query = relativeUrl["search?".Length..];
            var searchTerm = GetQueryStringValue(query, "q") ?? "search";
            var readableSearchTerm = SlugifyForFileName(searchTerm, 40);
            var shortHash = GetShortStableHash(relativeUrl);

            return Path.Combine(
                _jsonCacheRoot,
                "Search",
                $"{readableSearchTerm}--{shortHash}.json");
        }

        return Path.Combine(_jsonCacheRoot, $"{GetStableFileName(relativeUrl)}.json");
    }

    private bool TryReadCachedImage(string id, string size, out CatalogImageResult? result)
    {
        result = null;

        var folder = GetImageCacheFolder(id, size);
        var bytesPath = Path.Combine(folder, "image.bin");
        var contentTypePath = Path.Combine(folder, "content-type.txt");

        if (!File.Exists(bytesPath) || !File.Exists(contentTypePath))
            return false;

        var bytes = File.ReadAllBytes(bytesPath);
        var contentType = File.ReadAllText(contentTypePath);

        if (bytes.Length == 0 || string.IsNullOrWhiteSpace(contentType))
            return false;

        result = new CatalogImageResult(bytes, contentType.Trim());
        return true;
    }

    private void TryWriteCachedImage(string id, string size, CatalogImageResult result)
    {
        if (result.Bytes.Length == 0 || !ContentTypeLooksLikeImage(result.ContentType))
            return;

        var folder = GetImageCacheFolder(id, size);
        Directory.CreateDirectory(folder);

        File.WriteAllBytes(Path.Combine(folder, "image.bin"), result.Bytes);
        File.WriteAllText(Path.Combine(folder, "content-type.txt"), result.ContentType);
    }

    private string GetImageCacheFolder(string id, string size)
    {
        var safeSize = SanitizeFileSegment(size);
        var safeId = SlugifyForFileName(id.Trim(), 80);

        return Path.Combine(_imageCacheRoot, safeSize, safeId);
    }

    private static void UpdateRateLimitState(HttpResponseMessage response)
    {
        lock (RateLimitLock)
        {
            _remainingHourlyRequests = TryGetHeaderInt(response, "X-RateLimit-Remaining-Hour");
            _remainingDailyRequests = TryGetHeaderInt(response, "X-RateLimit-Remaining-Day");
            _rateLimitCapturedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static int? TryGetHeaderInt(HttpResponseMessage response, string headerName)
    {
        if (!response.Headers.TryGetValues(headerName, out var values))
            return null;

        var raw = values.FirstOrDefault();
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static bool ShouldAllowLiveImageFetch()
    {
        var configuredValue = Environment.GetEnvironmentVariable("POKESCOUT_ALLOW_LIVE_IMAGE_FETCH");

        if (string.Equals(configuredValue, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configuredValue, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lock (RateLimitLock)
        {
            if (_rateLimitCapturedAtUtc.HasValue &&
                _remainingHourlyRequests.HasValue &&
                _remainingHourlyRequests.Value <= MinimumRemainingHourlyRequestsForLiveImages)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContentTypeLooksLikeImage(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) &&
               contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }

    private static string GetStableFileName(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

    private static PriceSnapshot ExtractPriceSnapshot(JsonElement card)
    {
        if (card.TryGetProperty("tcgplayer", out var tcgplayer) &&
            tcgplayer.ValueKind == JsonValueKind.Object &&
            tcgplayer.TryGetProperty("prices", out var tcgPrices) &&
            tcgPrices.ValueKind == JsonValueKind.Array)
        {
            foreach (var price in tcgPrices.EnumerateArray())
            {
                return new PriceSnapshot(
                    GetDecimal(price, "market_price", "market"),
                    GetDecimal(price, "low_price", "low"),
                    GetDecimal(price, "mid_price", "mid"),
                    GetDecimal(price, "high_price", "high"),
                    GetDateTime(price, "updated_at", "updatedAt"));
            }
        }

        if (card.TryGetProperty("cardmarket", out var cardmarket) &&
            cardmarket.ValueKind == JsonValueKind.Object &&
            cardmarket.TryGetProperty("prices", out var cmPrices) &&
            cmPrices.ValueKind == JsonValueKind.Array)
        {
            foreach (var price in cmPrices.EnumerateArray())
            {
                return new PriceSnapshot(
                    GetDecimal(price, "avg", "trend", "low"),
                    GetDecimal(price, "low"),
                    GetDecimal(price, "trend", "avg"),
                    GetDecimal(price, "sell", "avg"),
                    GetDateTime(price, "updated_at", "updatedAt"));
            }
        }

        return new PriceSnapshot(null, null, null, null, null);
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

    private static string? GetJoinedStrings(JsonElement? obj, params string[] names)
    {
        if (obj is null || obj.Value.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (!obj.Value.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();

            if (value.ValueKind == JsonValueKind.Array)
            {
                var parts = value.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (parts.Count > 0)
                    return string.Join(", ", parts!);
            }
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
        var snapshot = ExtractPriceSnapshot(card);
        return snapshot.MarketPrice;
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

    private static DateTime? GetDateTime(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
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

    private static string? GetQueryStringValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(key))
            return null;

        var segments = query.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var parts = segment.Split('=', 2);
            var currentKey = Uri.UnescapeDataString(parts[0]);

            if (!string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            if (parts.Length == 1)
                return "";

            return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }

    private static string SlugifyForFileName(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "default";

        var normalized = value.Trim().ToLowerInvariant();
        normalized = Uri.UnescapeDataString(normalized);

        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (ch is '-' or '_' or ' ')
            {
                builder.Append('-');
            }
        }

        var cleaned = Regex.Replace(builder.ToString(), "-{2,}", "-").Trim('-');

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "default";

        if (cleaned.Length > maxLength)
            cleaned = cleaned[..maxLength].Trim('-');

        return cleaned;
    }

    private static string GetShortStableHash(string value)
    {
        var fullHash = GetStableFileName(value);
        return fullHash[..8];
    }

    private sealed record PriceSnapshot(
        decimal? MarketPrice,
        decimal? LowPrice,
        decimal? MidPrice,
        decimal? HighPrice,
        DateTime? UpdatedAtUtc);
}