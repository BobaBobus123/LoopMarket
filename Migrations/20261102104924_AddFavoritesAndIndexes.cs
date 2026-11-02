using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeStorm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoritesAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ListingId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favorites_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Favorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ListingId",
                table: "Reviews",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CreatedAt",
                table: "Listings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Price",
                table: "Listings",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status",
                table: "Listings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_ListingId",
                table: "Favorites",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId_ListingId",
                table: "Favorites",
                columns: new[] { "UserId", "ListingId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Listings_ListingId",
                table: "Reviews",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Listings_ListingId",
                table: "Reviews");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ListingId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Listings_CreatedAt",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Price",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Status",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ListingId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Listings");
        }
    }
}
