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

            migrationBuilder.AddColumn<int>(
                name: "RegistryId",
                table: "Domains",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
