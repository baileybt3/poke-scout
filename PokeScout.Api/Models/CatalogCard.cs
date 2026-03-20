namespace PokeScout.Api.Models
{
    public class CatalogCard
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // External source identity
        public string ExternalId { get; set; } = "";
        public string Name { get; set; } = "";

        // Set info
        public string SetName { get; set; } = "";
        public string SetApiId { get; set; } = "";
        public string SetCode { get; set; } = "";

        // Card details
        public string Number { get; set; } = "";
        public string Rarity { get; set; } = "";
        public string Supertype { get; set; } = "";
        public string Subtypes { get; set; } = "";

        // Images
        public string RemoteImageUrl { get; set; } = "";
        public string LocalImagePath { get; set; } = "";

        // Links
        public string TcgPlayerUrl { get; set; } = "";

        // Latest price snapshot
        public decimal? MarketPrice { get; set; }
        public decimal? LowPrice { get; set; }
        public decimal? MidPrice { get; set; }
        public decimal? HighPrice { get; set; }

        // Source freshness
        public DateTime? PriceUpdatedAtUtc { get; set; }
        public DateTime? ImageLastSyncedAtUtc { get; set; }
        public DateTime? CatalogLastSyncedAtUtc { get; set; }

        // App timestamps
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}