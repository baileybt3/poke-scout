using Microsoft.AspNetCore.Mvc;
using PokeScout.Api.Dtos;
using PokeScout.Api.Models;
using PokeScout.Api.Services;

namespace PokeScout.Api.Controllers
{
    [ApiController]
    [Route("api/cards")]
    public class CardsController : ControllerBase
    {
        private readonly ICardService _cards;
        public CardsController(ICardService cards) => _cards = cards;

        [HttpGet]
        public ActionResult<IEnumerable<Card>> GetAll([FromQuery] string? search = null)
                => Ok(_cards.GetAll(search));

        [HttpGet("{id:guid}")]
        public ActionResult<Card> GetById(Guid id)
        {
            var card = _cards.GetById(id);
            return card is null ? NotFound() : Ok(card);
        }

        [HttpPost]
        public ActionResult<Card> Create([FromBody] CreateCardRequest request)
        {
            var created = _cards.Create(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:guid}")]
        public ActionResult<Card> Update(Guid id, [FromBody] UpdateCardRequest request)
        {
            return _cards.Update(id, request, out var updated)
                ? Ok(updated)
                : NotFound();
        }

        [HttpDelete("{id:guid}")]
        public IActionResult Delete(Guid id)
            => _cards.Delete(id) ? NoContent() : NotFound();
    }
}
