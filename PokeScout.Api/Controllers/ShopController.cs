using Microsoft.AspNetCore.Mvc;
using PokeScout.Api.Dtos;
using PokeScout.Api.Services;

namespace PokeScout.Api.Controllers
{
    [ApiController]
    [Route("api/shop")]
    public class ShopController : ControllerBase
    {
        [HttpGet]
        public ActionResult<List<ShopCatalogItemDto>> GetAll()
        {
            return Ok(ShopCatalogSeed.GetItems());
        }
    }
}