#!/usr/bin/env bash
#
# Loads an exported chain into another xtzkt database.
#
# Usage: ./import-chain.sh <database> <dump-dir>
# Host/user/password are taken from the usual PGHOST/PGPORT/PGUSER/PGPASSWORD env vars.

set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "Usage: $0 <database> <dump-dir>" >&2
    exit 1
fi

db=$1
dir=$2

case $dir in
    *[\'\"\$\`]*) echo "$0: dump dir must not contain quotes, \$ or backticks" >&2; exit 1 ;;
esac

psql=(psql -X -v ON_ERROR_STOP=1 -d "$db")

mapfile -t tables < <("${psql[@]}" -Atq -c "
    SELECT DISTINCT c.table_name
    FROM information_schema.columns AS c
    JOIN information_schema.tables AS t
      ON t.table_schema = c.table_schema AND t.table_name = c.table_name
    WHERE c.table_schema = 'public'
      AND t.table_type = 'BASE TABLE'
      AND (c.table_name = 'Chains' OR (c.column_name = 'ChainId' AND c.data_type = 'integer'))
    ORDER BY 1")

((${#tables[@]} > 0)) || { echo "$0: no chain tables found in '$db'" >&2; exit 1; }

{
    printf '%s\n' "SET statement_timeout = 0;" "SET client_encoding = 'UTF8';" "BEGIN;"
    for tbl in "${tables[@]}"; do
        printf '%s\n' "\\copy \"$tbl\" FROM PROGRAM 'gzip -dc \"$dir/$tbl.csv.gz\"' WITH (FORMAT csv, HEADER match)"
    done
    printf '%s\n' "COMMIT;"
} | "${psql[@]}"

"${psql[@]}" -q <<'SQL'
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT quote_ident(c.relname) AS tbl, quote_ident(a.attname) AS col,
               pg_get_serial_sequence(quote_ident(c.relname), a.attname) AS seq
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
        WHERE n.nspname = 'public'
          AND c.relkind = 'r'
          AND pg_get_serial_sequence(quote_ident(c.relname), a.attname) IS NOT NULL
    LOOP
        -- GREATEST ignores NULLs, so an empty table or an untouched sequence leaves it alone
        EXECUTE format('SELECT setval(%L, GREATEST(max(%s), pg_sequence_last_value(%L::regclass))) FROM %s',
                       r.seq, r.col, r.seq, r.tbl);
    END LOOP;
END $$;
SQL
