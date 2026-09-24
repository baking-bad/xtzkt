using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Xtzkt.Data.Migrations
{
    /// <inheritdoc />
    public partial class Domains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DomainsLevel",
                table: "Chains");

            migrationBuilder.DropColumn(
                name: "DomainsNameRegistry",
                table: "Chains");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Domains",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstTimestamp",
                table: "Domains",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTimestamp",
                table: "Domains",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RegistryId",
                table: "Domains",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            Triggers.AddNotificationTrigger(migrationBuilder,
                name: "domain_changed",
                table: "Domains",
                columns: ["ChainId", "Name", "Address", "Reverse", "Expiration"],
                payload: @"COALESCE(NEW.""Id"", OLD.""Id"")::text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Triggers.RemoveNotificationTrigger(migrationBuilder, "domain_changed", "Domains");

            migrationBuilder.DropColumn(
                name: "FirstTimestamp",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "LastTimestamp",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "RegistryId",
                table: "Domains");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Domains",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "DomainsLevel",
                table: "Chains",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DomainsNameRegistry",
                table: "Chains",
                type: "text",
                nullable: true);
        }
    }
}
