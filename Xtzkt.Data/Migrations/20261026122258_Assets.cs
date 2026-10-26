using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xtzkt.Data.Migrations
{
    /// <inheritdoc />
    public partial class Assets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "Assets",
                type: "text",
                nullable: true);

            Triggers.AddNotificationTrigger(migrationBuilder,
                name: "asset_changed",
                table: "Assets",
                columns: null,
                payload: @"COALESCE(NEW.""Id"", OLD.""Id"")::text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Triggers.RemoveNotificationTrigger(migrationBuilder, "asset_changed", "Assets");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "Assets");
        }
    }
}
