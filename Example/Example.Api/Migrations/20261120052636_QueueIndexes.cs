using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class QueueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_available_after",
                table: "scheduled_messages",
                column: "available_after");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_messages_tag",
                table: "scheduled_messages",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "IX_poisoned_messages_created_at",
                table: "poisoned_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_consumer_messages_rail_id_available_after",
                table: "consumer_messages",
                columns: new[] { "rail_id", "available_after" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_scheduled_messages_available_after",
                table: "scheduled_messages");

            migrationBuilder.DropIndex(
                name: "IX_scheduled_messages_tag",
                table: "scheduled_messages");

            migrationBuilder.DropIndex(
                name: "IX_poisoned_messages_created_at",
                table: "poisoned_messages");

            migrationBuilder.DropIndex(
                name: "IX_consumer_messages_rail_id_available_after",
                table: "consumer_messages");
        }
    }
}
