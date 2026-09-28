using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xtzkt.Data.Migrations
{
    /// <inheritdoc />
    public partial class Profiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    LogoDark = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    Support = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Telegram = table.Column<string>(type: "text", nullable: true),
                    Discord = table.Column<string>(type: "text", nullable: true),
                    Reddit = table.Column<string>(type: "text", nullable: true),
                    Slack = table.Column<string>(type: "text", nullable: true),
                    Github = table.Column<string>(type: "text", nullable: true),
                    Gitlab = table.Column<string>(type: "text", nullable: true),
                    Mailchain = table.Column<string>(type: "text", nullable: true),
                    Instagram = table.Column<string>(type: "text", nullable: true),
                    Facebook = table.Column<string>(type: "text", nullable: true),
                    X = table.Column<string>(type: "text", nullable: true),
                    Docs = table.Column<string>(type: "text", nullable: true),
                    CommitDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommitHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            Triggers.AddNotificationTrigger(migrationBuilder,
                name: "profile_changed",
                table: "Profiles",
                columns: null,
                payload: @"COALESCE(NEW.""Type"", OLD.""Type"") || ':' || COALESCE(NEW.""Id"", OLD.""Id"")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Triggers.RemoveNotificationTrigger(migrationBuilder, "profile_changed", "Profiles");

            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
