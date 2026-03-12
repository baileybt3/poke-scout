namespace PokeScout.Web.Models
{
    public class ShopCatalogItem
    {
        public string Title { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Condition { get; set; } = "NM";
        public int StockCount { get; set; }
        public bool IsStripeReady { get; set; }

        public string ButtonText => IsStripeReady ? "Buy with Stripe" : "Stripe Coming Soon";
    }
}