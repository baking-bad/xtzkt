using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xtzkt.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProfileMailchain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Profiles"" DROP COLUMN IF EXISTS ""Mailchain"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mailchain",
                table: "Profiles",
                type: "text",
                nullable: true);
        }
    }
}
