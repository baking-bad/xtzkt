#!/usr/bin/env bash
#
# Deletes all data of a single chain from an xtzkt database, so it can be reindexed from scratch
# without touching the other chains. Tables are taken from the catalog (every public table with an
# integer "ChainId" column), so new tables are covered automatically.
#
# The "Chains" row is deleted by "Id" ("ChainId" there is the textual network id, not a reference):
# it holds the id counters, the indexing head and the metadata service cursor, and the indexer
# re-creates it, zeroed, on the next start. "Assets" is left alone - it is curated cross-chain data.
#
# There are no foreign keys in the schema, so the deletion order doesn't matter. Everything runs in
# a single transaction. Stop the indexer of that chain first.
#
# Usage: ./drop-chain.sh <database> <chain-id>
# Host/user/password are taken from the usual PGHOST/PGPORT/PGUSER/PGPASSWORD env vars.

if [[ $# -ne 2 || ! $2 =~ ^[0-7]$ ]]; then
    echo "Usage: $0 <database> <chain-id 0-7>" >&2
    exit 1
fi

psql -X -v ON_ERROR_STOP=1 -d "$1" -v chain="$2" <<'SQL'
SET statement_timeout = 0;
SET xtzkt.chain = :'chain';

DO $$
DECLARE
    chain int := current_setting('xtzkt.chain')::int;
    tbl text;
BEGIN
    FOR tbl IN
        SELECT c.table_name
        FROM information_schema.columns AS c
        JOIN information_schema.tables AS t
          ON t.table_schema = c.table_schema AND t.table_name = c.table_name
        WHERE c.table_schema = 'public'
          AND t.table_type = 'BASE TABLE'
          AND c.column_name = 'ChainId'
          AND c.data_type = 'integer'
        ORDER BY c.table_name
    LOOP
        EXECUTE format('DELETE FROM %I WHERE "ChainId" = $1', tbl) USING chain;
        RAISE NOTICE 'table % cleared', tbl;
    END LOOP;

    DELETE FROM "Chains" WHERE "Id" = chain;
    RAISE NOTICE 'chain % dropped', chain;
END $$;
SQL
