using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xtzkt.Data.Migrations
{   
    /// <inheritdoc />
    public partial class Triggers : Migration
    {
        #region static
        public static void AddNotificationTrigger(MigrationBuilder builder, string name, string table, string[] columns, string payload)
        {
            builder.Sql($@"
                CREATE OR REPLACE FUNCTION notify_{name}() RETURNS TRIGGER AS $$
                    BEGIN
                    PERFORM pg_notify('{name}', {payload});
                    RETURN null;
                    END;
                $$ LANGUAGE plpgsql;");

            builder.Sql($@"
                CREATE TRIGGER {name}
                    AFTER UPDATE OF {string.Join(", ", columns.Select(x => $@"""{x}"""))} ON ""{table}""
                    FOR EACH ROW
                    WHEN ({string.Join(" OR ", columns.Select(x => $@"OLD.""{x}"" IS DISTINCT FROM NEW.""{x}"""))})
                    EXECUTE FUNCTION notify_{name}();");
        }

        public static void RemoveNotificationTrigger(MigrationBuilder builder, string name, string table)
        {
            builder.Sql($@"DROP TRIGGER IF EXISTS {name} ON ""{table}"" CASCADE");
            builder.Sql($@"DROP FUNCTION IF EXISTS notify_{name} CASCADE");
        }
        #endregion

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddNotificationTrigger(migrationBuilder,
                name: "chain_state_changed",
                table: "Chains",
                columns: ["Hash"],
                payload: @"NEW.""Id"" || ':' || NEW.""Level""");

            AddNotificationTrigger(migrationBuilder,
                name: "chain_sync_state_changed",
                table: "Chains",
                columns: ["KnownLevel", "SyncedAt"],
                payload: @"NEW.""Id"" || ':' || NEW.""KnownLevel"" || ':' || NEW.""SyncedAt"""); // ISO 8601 (1997-12-17 07:37:16)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RemoveNotificationTrigger(migrationBuilder, "chain_state_changed", "Chains");
            RemoveNotificationTrigger(migrationBuilder, "chain_sync_state_changed", "Chains");
        }
    }
}
