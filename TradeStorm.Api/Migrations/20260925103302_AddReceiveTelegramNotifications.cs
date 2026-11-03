using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeStorm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiveTelegramNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceiveTelegramNotifications",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiveTelegramNotifications",
                table: "Users");
        }
    }
}
