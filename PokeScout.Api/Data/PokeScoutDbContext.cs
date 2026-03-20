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
        public DbSet<CatalogCard> CatalogCards => Set<CatalogCard>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Card>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.ExternalId)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Name)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Set)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.SetApiId)
                    .HasMaxLength(100);

                entity.Property(x => x.SetCode)
                    .HasMaxLength(100);

                entity.Property(x => x.Number)
                    .HasMaxLength(50);

                entity.Property(x => x.Rarity)
                    .HasMaxLength(100);

                entity.Property(x => x.RemoteImageUrl)
                    .HasMaxLength(1000);

                entity.Property(x => x.LocalImagePath)
                    .HasMaxLength(1000);

                entity.Property(x => x.Notes)
                    .HasMaxLength(2000);

                entity.Property(x => x.MarketPrice)
                    .HasPrecision(10, 2);

                entity.Property(x => x.LowPrice)
                    .HasPrecision(10, 2);

                entity.Property(x => x.MidPrice)
                    .HasPrecision(10, 2);

                entity.Property(x => x.HighPrice)
                    .HasPrecision(10, 2);

                entity.HasIndex(x => x.ExternalId);
                entity.HasIndex(x => x.Name);
                entity.HasIndex(x => x.Set);
                entity.HasIndex(x => x.SetApiId);
                entity.HasIndex(x => new { x.SetApiId, x.Number });
                entity.HasIndex(x => x.PriceUpdatedAtUtc);
                entity.HasIndex(x => x.CatalogLastSyncedAtUtc);
            });

            modelBuilder.Entity<CatalogCard>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.ExternalId)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Name)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.SetName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.SetApiId)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.SetCode)
                    .HasMaxLength(100);

                entity.Property(x => x.Number)
                    .HasMaxLength(50);

                entity.Property(x => x.Rarity)
                    .HasMaxLength(100);

                entity.Property(x => x.Supertype)
                    .HasMaxLength(100);

                entity.Property(x => x.Subtypes)
                    .HasMaxLength(500);

                entity.Property(x => x.RemoteImageUrl)
                    .HasMaxLength(1000);

                entity.Property(x => x.LocalImagePath)
                    .HasMaxLength(1000);

                entity.Property(x => x.TcgPlayerUrl)
                    .HasMaxLength(1000);

                entity.Property(x => x.MarketPrice)
                    .HasPrecision(10, 2);

                entity.Property(x => x.LowPrice)
                    .HasPrecision(10, 2);

                entity.Property(x => x.MidPrice)
                    .HasPrecision(10, 2);

                entity.Property(x => x.HighPrice)
                    .HasPrecision(10, 2);

                entity.HasIndex(x => x.ExternalId)
                    .IsUnique();

                entity.HasIndex(x => x.Name);
                entity.HasIndex(x => x.SetName);
                entity.HasIndex(x => x.SetApiId);
                entity.HasIndex(x => new { x.SetApiId, x.Number });
                entity.HasIndex(x => x.PriceUpdatedAtUtc);
                entity.HasIndex(x => x.CatalogLastSyncedAtUtc);
            });
        }
    }
}