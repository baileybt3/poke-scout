namespace PokeScout.Web.Models
{
    public class ShopCatalogItemDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Tier { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public string Condition { get; set; } = "NM";
        public int StockCount { get; set; }
        public bool IsStripeReady { get; set; }
        public string? ImageUrl { get; set; }

        public string ButtonText => IsStripeReady ? "Buy with Stripe" : "Stripe Coming Soon";
    }
}