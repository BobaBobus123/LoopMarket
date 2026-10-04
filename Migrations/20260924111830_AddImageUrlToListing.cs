using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeStorm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlToListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Listings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Listings");
        }
    }
}
