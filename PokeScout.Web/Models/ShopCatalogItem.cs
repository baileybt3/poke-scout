namespace PokeScout.Web.Models
{
    public class ShopCatalogItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Tier { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public string Condition { get; set; } = "NM";
        public int StockCount { get; set; }
        public bool IsStripeReady { get; set; }
        public string ButtonText { get; set; } = "Buy Now";
        public string? ImageUrl { get; set; }
    }
}