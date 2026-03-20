namespace PokeScout.Api.Models
{
    public class Card
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Catalog identity
        public string ExternalId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Set { get; set; } = "";
        public string SetApiId { get; set; } = "";
        public string SetCode { get; set; } = "";
        public string Number { get; set; } = "";
        public string Rarity { get; set; } = "";

        // Image storage
        public string RemoteImageUrl { get; set; } = "";
        public string LocalImagePath { get; set; } = "";

        // Price snapshot
        public decimal? MarketPrice { get; set; }
        public decimal? LowPrice { get; set; }
        public decimal? MidPrice { get; set; }
        public decimal? HighPrice { get; set; }
        public DateTime? PriceUpdatedAtUtc { get; set; }

        // Inventory-specific fields
        public CardCondition Condition { get; set; } = CardCondition.NM;
        public int Quantity { get; set; } = 1;
        public string? Notes { get; set; }

        // Sync tracking
        public DateTime? CatalogLastSyncedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}