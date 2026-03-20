using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeScout.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCardForLocalCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CatalogLastSyncedAtUtc",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "HighPrice",
                table: "Cards",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalImagePath",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "LowPrice",
                table: "Cards",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketPrice",
                table: "Cards",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MidPrice",
                table: "Cards",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceUpdatedAtUtc",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RemoteImageUrl",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SetApiId",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SetCode",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogLastSyncedAtUtc",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "HighPrice",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LocalImagePath",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LowPrice",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "MarketPrice",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "MidPrice",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PriceUpdatedAtUtc",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "RemoteImageUrl",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "SetApiId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "SetCode",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Cards");
        }
    }
}
