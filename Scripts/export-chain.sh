#!/usr/bin/env bash
#
# Exports all data of one chain into ./chain-<id>/<table>.csv.gz.
#
# Usage: ./export-chain.sh <database> <chain-id>
# Host/user/password are taken from the usual PGHOST/PGPORT/PGUSER/PGPASSWORD env vars.

set -euo pipefail

if [[ $# -ne 2 || ! $2 =~ ^[0-7]$ ]]; then
    echo "Usage: $0 <database> <chain-id 0-7>" >&2
    exit 1
fi

db=$1
chain=$2
dir=chain-$chain

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

mkdir -p "$dir"

{
    printf '%s\n' "SET statement_timeout = 0;" "SET client_encoding = 'UTF8';" "BEGIN ISOLATION LEVEL REPEATABLE READ;"
    for tbl in "${tables[@]}"; do
        if [[ $tbl == Chains ]]; then
            where="\"Id\" = $chain"
        else
            where="\"ChainId\" = $chain"
        fi
        printf '%s\n' "\\copy (SELECT * FROM \"$tbl\" WHERE $where) TO PROGRAM 'gzip > \"$dir/$tbl.csv.gz\"' WITH (FORMAT csv, HEADER)"
    done
    printf '%s\n' "COMMIT;"
} | "${psql[@]}"
