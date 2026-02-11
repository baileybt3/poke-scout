using Microsoft.EntityFrameworkCore;
using PokeScout.Api.Models;

namespace PokeScout.Api.Data
{
    public class PokeScoutDbContext : DbContext
    {
        public PokeScoutDbContext(DbContextOptions<PokeScoutDbContext> options) : base(options)
        {

        }

        public DbSet<Card> Cards => Set<Card>();
    }
}
