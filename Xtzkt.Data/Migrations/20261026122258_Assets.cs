using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Assets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

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

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Assets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
