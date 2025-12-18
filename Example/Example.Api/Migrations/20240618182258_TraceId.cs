using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Migrations
{
    /// <inheritdoc />
    public partial class TraceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "span_id",
                table: "poisoned_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                table: "poisoned_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "span_id",
                table: "consumer_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                table: "consumer_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "span_id",
                table: "poisoned_messages");

            migrationBuilder.DropColumn(
                name: "trace_id",
                table: "poisoned_messages");

            migrationBuilder.DropColumn(
                name: "span_id",
                table: "consumer_messages");

            migrationBuilder.DropColumn(
                name: "trace_id",
                table: "consumer_messages");
        }
    }
}
