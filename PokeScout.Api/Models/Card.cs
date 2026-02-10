namespace PokeScout.Api.Models
{
    public class Card
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Set { get; set; } = "";
        public CardCondition Condition { get; set; } = CardCondition.NM;
        public int Quantity { get; set; } = 1;
        public string? Notes { get; set; }

    }
}
