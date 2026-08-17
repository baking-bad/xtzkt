<#
    Exports all data of one chain into ./chain-<id>/<table>.csv.gz.

    Usage: .\export-chain.ps1 <database> <chain-id>
    Host/user/password are taken from the usual PGHOST/PGPORT/PGUSER/PGPASSWORD env vars.
#>

param(
    [Parameter(Mandatory = $true)][string] $Database,
    [Parameter(Mandatory = $true)][ValidateRange(0, 7)][int] $ChainId
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

$tables = @(& psql @psqlArgs -Atq -c $query)
if ($LASTEXITCODE -ne 0) { throw "psql exited with $LASTEXITCODE" }
if ($tables.Count -eq 0) { throw "no chain tables found in '$Database'" }

$dir = Join-Path (Get-Location).Path "chain-$ChainId"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$lines = @("SET statement_timeout = 0;", "SET client_encoding = 'UTF8';", "BEGIN ISOLATION LEVEL REPEATABLE READ;")

foreach ($table in $tables) {
    $where = if ($table -eq 'Chains') { """Id"" = $ChainId" } else { """ChainId"" = $ChainId" }
    $lines += "\copy (SELECT * FROM ""$table"" WHERE $where) TO '$dir\$table.csv' WITH (FORMAT csv, HEADER)"
}

$lines += "COMMIT;"

$file = Join-Path ([IO.Path]::GetTempPath()) "export-chain-$PID.psql"
[IO.File]::WriteAllText($file, ($lines -join "`n") + "`n", (New-Object Text.UTF8Encoding $false))

try {
    & psql @psqlArgs -f $file
    if ($LASTEXITCODE -ne 0) { throw "psql exited with $LASTEXITCODE" }
}
finally {
    Remove-Item $file
}

foreach ($table in $tables) {
    $csv = Join-Path $dir "$table.csv"
    $source = [IO.File]::OpenRead($csv)
    $target = [IO.File]::Create("$csv.gz")
    $gzip = New-Object IO.Compression.GZipStream($target, [IO.Compression.CompressionMode]::Compress)
    try { $source.CopyTo($gzip) } finally { $gzip.Dispose(); $target.Dispose(); $source.Dispose() }
    Remove-Item $csv
}
