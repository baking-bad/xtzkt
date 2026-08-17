#!/usr/bin/env bash
#
# Deletes all data of a single chain from an xtzkt database.
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
