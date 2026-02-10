using PokeScout.Api.Dtos;
using PokeScout.Api.Models;

namespace PokeScout.Api.Services
{
    public interface ICardService
    {
        IEnumerable<Card> GetAll(string? search = null);
        Card? GetById(Guid id);
        Card Create(CreateCardRequest request);
        bool Update(Guid id, UpdateCardRequest request, out Card? updated);
        bool Delete(Guid id);
    }
}
