using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class RailIdAndLastError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "poisoned_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rail_id",
                table: "poisoned_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "consumer_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rail_id",
                table: "consumer_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_error",
                table: "poisoned_messages");

            migrationBuilder.DropColumn(
                name: "rail_id",
                table: "poisoned_messages");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "consumer_messages");

            migrationBuilder.DropColumn(
                name: "rail_id",
                table: "consumer_messages");
        }
    }
}
