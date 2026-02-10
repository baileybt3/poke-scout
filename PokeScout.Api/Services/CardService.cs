using System.Collections.Concurrent;
using PokeScout.Api.Dtos;
using PokeScout.Api.Models;

namespace PokeScout.Api.Services
{
    public class CardService : ICardService
    {
        private readonly ConcurrentDictionary<Guid, Card> _cards = new();

        public IEnumerable<Card> GetAll(string? search = null)
        {
            var items = _cards.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                items = items.Where(c =>
                    c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    c.Set.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            return items.OrderBy(c => c.Name);
        }

        public Card? GetById(Guid id) => _cards.TryGetValue(id, out var card) ? card : null;

        public Card Create(CreateCardRequest request)
        {
            var card = new Card
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Set = request.Set.Trim(),
                Condition = request.Condition,
                Quantity = request.Quantity,
                Notes = request.Notes
            };

            _cards[card.Id] = card;
            return card;
        }

        public bool Update(Guid id, UpdateCardRequest request, out Card? updated)
        {
            updated = null;

            if(!_cards.TryGetValue(id, out var existing))
            {
                return false;
            }

            existing.Name = request.Name.Trim();
            existing.Set = request.Set.Trim();
            existing.Condition = request.Condition;
            existing.Quantity = request.Quantity;
            existing.Notes = request.Notes;

            updated = existing;
            return true;
        }

        public bool Delete(Guid id) => _cards.TryRemove(id, out _); 
    }
}
