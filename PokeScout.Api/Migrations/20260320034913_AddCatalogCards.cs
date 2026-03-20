using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeScout.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SetApiId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SetCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Supertype = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subtypes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RemoteImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LocalImagePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TcgPlayerUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MarketPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    LowPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MidPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    HighPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PriceUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImageLastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CatalogLastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogCards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCards_CatalogLastSyncedAtUtc",
                table: "CatalogCards",
                column: "CatalogLastSyncedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCards_ExternalId",
                table: "CatalogCards",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCards_Name",
                table: "CatalogCards",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCards_PriceUpdatedAtUtc",
                table: "CatalogCards",
                column: "PriceUpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCards_SetApiId",
                table: "CatalogCards",
                column: "SetApiId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCards_SetApiId_Number",
                table: "CatalogCards",
                columns: new[] { "SetApiId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCards_SetName",
                table: "CatalogCards",
                column: "SetName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogCards");
        }
    }
}
