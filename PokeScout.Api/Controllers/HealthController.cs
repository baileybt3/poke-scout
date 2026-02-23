using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PokeScout.Api.Data;

namespace PokeScout.Api.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly PokeScoutDbContext _db;

        public HealthController(PokeScoutDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Get() => Ok(new { status = "ok" });

        [HttpGet("db")]
        public async Task<IActionResult> Db()
        {
            var canConnect = await _db.Database.CanConnectAsync();
            return Ok(new { database = canConnect ? "ok" : "down" });
        }
    }
}
