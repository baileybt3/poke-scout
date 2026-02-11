using Microsoft.EntityFrameworkCore;
using PokeScout.Api.Data;
using PokeScout.Api.Dtos;
using PokeScout.Api.Models;

namespace PokeScout.Api.Services
{
    public class EfCardService : ICardService
    {
        private readonly PokeScoutDbContext _db;

        public EfCardService(PokeScoutDbContext db) => _db = db;

        public IEnumerable<Card> GetAll(string? search = null)
        {
            var query = _db.Cards.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(s) ||
                    c.Set.ToLower().Contains(s));
            }

            return query.OrderBy(c => c.Name).ToList();
        }

        public Card? GetById(Guid id) => _db.Cards.FirstOrDefault(c => c.Id == id);

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

            _db.Cards.Add(card);
            _db.SaveChanges();
            return card;
        }

        public bool Update(Guid id, UpdateCardRequest request, out Card? updated)
        {
            updated = _db.Cards.FirstOrDefault(c => c.Id == id);
            if (updated is null) return false;

            updated.Name = request.Name.Trim();
            updated.Set = request.Set.Trim();
            updated.Condition = request.Condition;
            updated.Quantity = request.Quantity;
            updated.Notes = request.Notes;

            _db.SaveChanges();
            return true;
        }

        public bool Delete(Guid id)
        {
            var card = _db.Cards.FirstOrDefault(c => c.Id == id);
            if (card is null) return false;

            _db.Cards.Remove(card);
            _db.SaveChanges();
            return true;
        }
    }
}
