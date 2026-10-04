using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeStorm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOlxFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "Listings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShippingAvailable",
                table: "Listings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Listings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "IsShippingAvailable",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Listings");
        }
    }
}
