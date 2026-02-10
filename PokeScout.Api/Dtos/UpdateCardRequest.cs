using System.ComponentModel.DataAnnotations;
using PokeScout.Api.Models;

namespace PokeScout.Api.Dtos
{
    public class UpdateCardRequest
    {
        [Required]
        [MinLength(1)]
        public string Name { get; set; } = "";
        public string Set { get; set; } = "";
        public CardCondition Condition { get; set; } = CardCondition.NM;

        [Range(1, 999)]
        public int Quantity { get; set; } = 1;
        public string? Notes { get; set; }
    }
}
