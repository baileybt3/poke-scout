namespace PokeScout.Api.Dtos;

public sealed class CatalogSearchResultDto
{
    public string ExternalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string SetName { get; set; } = "";
    public string Series { get; set; } = "";
    public string Rarity { get; set; } = "";
    public decimal? MarketPrice { get; set; }
    public string TcgPlayerUrl { get; set; } = "";
}