<#
.SYNOPSIS
    READ-ONLY metadata export for the existing SMC_BhoomiMalmatta database.

.DESCRIPTION
    Connects with Windows Authentication, runs Get-SchemaMetadata.sql (catalog views only)
    and writes database-schema.json, database-schema.csv and database-relationships.csv.
    It never reads business tables and never modifies the database.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Export-SchemaMetadata.ps1

.EXAMPLE
    # Named instance
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Export-SchemaMetadata.ps1 -Server "localhost\SQLEXPRESS"
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [ValidateSet('SMC_BhoomiMalmatta')]
    [string]$Database = 'SMC_BhoomiMalmatta',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'output')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

$sqlPath = Join-Path $PSScriptRoot 'Get-SchemaMetadata.sql'
if (-not (Test-Path -LiteralPath $sqlPath)) { throw "Missing file: $sqlPath" }

# ---- Safety check: the SQL file must be SELECT/SET only --------------------------------------
$sqlText  = Get-Content -LiteralPath $sqlPath -Raw -Encoding UTF8
$stripped = [regex]::Replace($sqlText, '/\*.*?\*/', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
$stripped = [regex]::Replace($stripped, '--[^\r\n]*', '')
$forbidden = '\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE|DENY|BACKUP|RESTORE|SHUTDOWN|DBCC|OPENROWSET|OPENQUERY|OPENDATASOURCE|INTO)\b'
$hit = [regex]::Match($stripped, $forbidden, 'IgnoreCase')
if ($hit.Success) { throw "Safety check failed: forbidden keyword '$($hit.Value)' found in Get-SchemaMetadata.sql. Nothing was executed." }

$resultNames = @('databaseInfo','schemas','tables','columns','primaryKeys','foreignKeys',
                 'uniqueConstraints','defaultConstraints','indexes','computedColumns',
                 'checkConstraints','triggers','views')

function ConvertTo-RecordList {
    param([System.Data.DataTable]$Table)
    $list = New-Object System.Collections.Generic.List[object]
    foreach ($row in $Table.Rows) {
        $rec = [ordered]@{}
        foreach ($col in $Table.Columns) {
            $v = $row[$col.ColumnName]
            if ($v -is [System.DBNull]) { $v = $null }
            elseif ($v -is [datetime])  { $v = $v.ToString('o') }
            $rec[$col.ColumnName] = $v
        }
        $list.Add([pscustomobject]$rec)
    }
    return ,$list
}

$connString = "Server=$Server;Database=$Database;Integrated Security=SSPI;Encrypt=True;TrustServerCertificate=True;" +
              "ApplicationIntent=ReadOnly;Application Name=SMC-SchemaDiscovery;Connection Timeout=15"

Write-Host "Server   : $Server"
Write-Host "Database : $Database"
Write-Host "Auth     : Windows Authentication"
Write-Host "Mode     : READ-ONLY metadata (sys.* catalog views only)"
Write-Host ""

$ds   = New-Object System.Data.DataSet
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText    = $sqlText
    $cmd.CommandTimeout = 120
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    [void]$adapter.Fill($ds)
}
finally {
    if ($conn.State -ne 'Closed') { $conn.Close() }
    $conn.Dispose()
}

if ($ds.Tables.Count -ne $resultNames.Count) {
    throw "Expected $($resultNames.Count) result sets but received $($ds.Tables.Count). Nothing was written."
}

$data = [ordered]@{}
for ($i = 0; $i -lt $resultNames.Count; $i++) {
    $data[$resultNames[$i]] = ConvertTo-RecordList -Table $ds.Tables[$i]
}

$info = $data['databaseInfo'][0]
if ($null -eq $info -or $info.database_name -ne 'SMC_BhoomiMalmatta') {
    throw "Connected database name is not SMC_BhoomiMalmatta. Nothing was written."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# ---- database-schema.json -------------------------------------------------------------------
$json = [ordered]@{
    exportInfo = [ordered]@{
        tool            = 'SMC-SchemaDiscovery 1.0'
        mode            = 'read-only catalog metadata; no business records read'
        server          = $Server
        database        = $Database
        exportedAtUtc   = $info.exported_at_utc
    }
}
foreach ($k in $resultNames) { $json[$k] = $data[$k] }
$jsonPath = Join-Path $OutputDirectory 'database-schema.json'
[System.IO.File]::WriteAllText($jsonPath, ($json | ConvertTo-Json -Depth 6), $utf8NoBom)

# ---- database-schema.csv (one row per column) ------------------------------------------------
$csvSchemaPath = Join-Path $OutputDirectory 'database-schema.csv'
$data['columns'] | Export-Csv -LiteralPath $csvSchemaPath -NoTypeInformation -Encoding UTF8

# ---- database-relationships.csv (one row per FK column pair) ---------------------------------
$csvRelPath = Join-Path $OutputDirectory 'database-relationships.csv'
$data['foreignKeys'] | Export-Csv -LiteralPath $csvRelPath -NoTypeInformation -Encoding UTF8

# ---- Summary --------------------------------------------------------------------------------
$tables = @($data['tables'])
$mr    = @($tables | Where-Object { $_.table_category -eq 'MR' }).Count
$tr    = @($tables | Where-Object { $_.table_category -eq 'TR' }).Count
$other = @($tables | Where-Object { $_.table_category -eq 'OTHER' })
$fkCount = @($data['foreignKeys'] | Select-Object -ExpandProperty foreign_key_name -Unique).Count

Write-Host "Export complete." -ForegroundColor Green
Write-Host ("  Tables: {0}  (MR_: {1}, TR_: {2}, other: {3})" -f $tables.Count, $mr, $tr, $other.Count)
Write-Host ("  Columns: {0}   Foreign keys: {1}   Indexes (rows): {2}" -f @($data['columns']).Count, $fkCount, @($data['indexes']).Count)
if ($other.Count -gt 0) {
    Write-Host "  Tables without MR_/TR_ prefix: $((($other | ForEach-Object { $_.table_name }) -join ', '))" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Files written to: $OutputDirectory"
Write-Host "  $jsonPath"
Write-Host "  $csvSchemaPath"
Write-Host "  $csvRelPath"
