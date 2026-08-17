<#
    Deletes all data of a single chain from an xtzkt database.

    Usage: .\drop-chain.ps1 <database> <chain-id>
    Host/user/password are taken from the usual PGHOST/PGPORT/PGUSER/PGPASSWORD env vars.
#>

param(
    [Parameter(Mandatory = $true)][string] $Database,
    [Parameter(Mandatory = $true)][ValidateRange(0, 7)][int] $ChainId
)

$ErrorActionPreference = 'Stop'

$sql = @'
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
'@

$file = Join-Path ([IO.Path]::GetTempPath()) "drop-chain-$PID.psql"
[IO.File]::WriteAllText($file, $sql, (New-Object Text.UTF8Encoding $false))

try {
    & psql -X -v ON_ERROR_STOP=1 -d $Database -v "chain=$ChainId" -f $file
}
finally {
    Remove-Item $file
}

exit $LASTEXITCODE
