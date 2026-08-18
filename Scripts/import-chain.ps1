<#
    Loads an exported chain into another xtzkt database.

    Usage: .\import-chain.ps1 <database> <dump-dir>
    Host/user/password are taken from the usual PGHOST/PGPORT/PGUSER/PGPASSWORD env vars.
#>

param(
    [Parameter(Mandatory = $true)][string] $Database,
    [Parameter(Mandatory = $true)][string] $DumpDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$psqlArgs = @('-X', '-v', 'ON_ERROR_STOP=1', '-d', $Database)

$query = @'
SELECT DISTINCT c.table_name
FROM information_schema.columns AS c
JOIN information_schema.tables AS t
  ON t.table_schema = c.table_schema AND t.table_name = c.table_name
WHERE c.table_schema = 'public'
  AND t.table_type = 'BASE TABLE'
  AND (c.table_name = 'Chains' OR (c.column_name = 'ChainId' AND c.data_type = 'integer'))
ORDER BY 1
'@

$sequences = @'
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
'@

$dir = (Resolve-Path -LiteralPath $DumpDir).Path

$tables = @(& psql @psqlArgs -Atq -c $query)
if ($LASTEXITCODE -ne 0) { throw "psql exited with $LASTEXITCODE" }
if ($tables.Count -eq 0) { throw "no chain tables found in '$Database'" }

$temp = Join-Path ([IO.Path]::GetTempPath()) "import-chain-$PID"
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    foreach ($table in $tables) {
        $source = [IO.File]::OpenRead((Join-Path $dir "$table.csv.gz"))
        $gzip = New-Object IO.Compression.GZipStream($source, [IO.Compression.CompressionMode]::Decompress)
        $target = [IO.File]::Create((Join-Path $temp "$table.csv"))
        try { $gzip.CopyTo($target) } finally { $target.Dispose(); $gzip.Dispose(); $source.Dispose() }
    }

    $lines = @("SET statement_timeout = 0;", "SET client_encoding = 'UTF8';", "BEGIN;")

    foreach ($table in $tables) {
        $lines += "\copy ""$table"" FROM '$temp\$table.csv' WITH (FORMAT csv, HEADER match)"
    }

    $lines += "COMMIT;"
    $lines += $sequences

    $file = Join-Path $temp 'import-chain.psql'
    [IO.File]::WriteAllText($file, ($lines -join "`n") + "`n", (New-Object Text.UTF8Encoding $false))

    & psql @psqlArgs -f $file
    if ($LASTEXITCODE -ne 0) { throw "psql exited with $LASTEXITCODE" }
}
finally {
    Remove-Item $temp -Recurse -Force
}
