-- The minimal set of indexes the API needs to *function*. It deliberately carries nothing for the filters
-- and sorts of individual endpoints - which of those an instance needs is the operator's decision,
-- not the repository's. An example of a working set lives next to this file in init.example.pgsql -
-- copy it, adjust it to what your instance serves, and point Db.InitScript to it.
--
-- Conventions:
--   * a blank line separates statements - not the semicolon;
--   * the whole script runs on a single connection, so a SET at the top applies to all of it;
--   * statements are grouped by table, one group per table;
--   * name indexes AX_*, so they never collide with the indexers' IX_*;
--   * use IF NOT EXISTS to avoid redundant work;
--   * build CONCURRENTLY to avoid blocking indexers;
--   * failed build can leave an INVALID index, which IF NOT EXISTS then skips forever - drop it manually.
--

-- the api caps statement_timeout, index builds must not be
SET statement_timeout = 0;

-- Addresses

-- AddressCache refreshes the addresses touched since the last notification, on every block
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_ChainId_LastLevel"
    ON "Addresses" ("ChainId", "LastLevel");

-- ProfileCache loads every profile name at startup
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Addresses_Extras"
    ON "Addresses" USING gin ("Extras" jsonb_path_ops)
    WHERE "Extras" IS NOT NULL;

-- Blocks

-- BlockCache resolves a timestamp filter or cursor into a block's id window
CREATE INDEX CONCURRENTLY IF NOT EXISTS "AX_Blocks_Timestamp_Id"
    ON "Blocks" ("Timestamp", "Id");
