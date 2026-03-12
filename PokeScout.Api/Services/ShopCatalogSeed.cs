using PokeScout.Api.Dtos;

namespace PokeScout.Api.Services
{
    public static class ShopCatalogSeed
    {
        public static List<ShopCatalogItemDto> GetItems()
        {
            return new List<ShopCatalogItemDto>
            {
                new ShopCatalogItemDto
                {
                    Id = "kids-starter-deck",
                    Title = "Kids Starter Deck",
                    Tier = "Kids",
                    Description = "Simple beginner-friendly deck with easy cards and clean gameplay.",
                    Price = 14.99m,
                    Condition = "LP-NM",
                    StockCount = 3,
                    IsStripeReady = false,
                    ImageUrl = null
                },
                new ShopCatalogItemDto
                {
                    Id = "league-ready-deck",
                    Title = "League Ready Deck",
                    Tier = "Intermediate",
                    Description = "A stronger deck for players who want better strategy and consistency.",
                    Price = 34.99m,
                    Condition = "NM",
                    StockCount = 2,
                    IsStripeReady = true,
                    ImageUrl = null
                },
                new ShopCatalogItemDto
                {
                    Id = "meta-copy-deck",
                    Title = "Meta Copy Deck",
                    Tier = "Competitive",
                    Description = "A polished competitive deck placeholder for your premium builds.",
                    Price = 69.99m,
                    Condition = "NM",
                    StockCount = 1,
                    IsStripeReady = false,
                    ImageUrl = null
                }
            };
        }
    }
}