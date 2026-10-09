<#
.SYNOPSIS
    READ-ONLY probe of lookup vocabulary and master configuration for SMC_BhoomiMalmatta.
.DESCRIPTION
    Runs Get-VocabularyProbe.sql (SELECT/SET only) with Windows Authentication and writes
    output\application-vocabulary.json. Reads code/status columns (distinct values + counts) and
    master configuration rows only. No personal data, remarks, files or password hashes.
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Export-VocabularyProbe.ps1
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [ValidateSet('SMC_BhoomiMalmatta')][string]$Database = 'SMC_BhoomiMalmatta',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'output')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

$sqlPath = Join-Path $PSScriptRoot 'Get-VocabularyProbe.sql'
$sqlText = Get-Content -LiteralPath $sqlPath -Raw -Encoding UTF8
$s = [regex]::Replace($sqlText, '/\*.*?\*/', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
$s = [regex]::Replace($s, '--[^\r\n]*', '')
$forbidden = '\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE|DENY|BACKUP|RESTORE|SHUTDOWN|DBCC|OPENROWSET|OPENQUERY|OPENDATASOURCE|INTO)\b'
$m = [regex]::Match($s, $forbidden, 'IgnoreCase')
if ($m.Success) { throw "Safety check failed: forbidden keyword '$($m.Value)'. Nothing was executed." }

Write-Host "This probe reads DISTINCT CODE/STATUS VALUES and MASTER CONFIGURATION rows only (no personal data)."
Write-Host "Review Get-VocabularyProbe.sql first if you wish."
if ((Read-Host "Type YES to continue") -ne 'YES') { Write-Host "Cancelled. Nothing was run."; return }

$names = @('vocabulary','serviceRates','services','businessTypes','numberConfig')
function ConvertTo-RecordList {
    param([System.Data.DataTable]$Table)
    $list = New-Object System.Collections.Generic.List[object]
    foreach ($row in $Table.Rows) {
        $rec = [ordered]@{}
        foreach ($col in $Table.Columns) {
            $v = $row[$col.ColumnName]
            if ($v -is [System.DBNull]) { $v = $null } elseif ($v -is [datetime]) { $v = $v.ToString('o') }
            $rec[$col.ColumnName] = $v
        }
        $list.Add([pscustomobject]$rec)
    }
    return ,$list
}

$cs = "Server=$Server;Database=$Database;Integrated Security=SSPI;Encrypt=True;TrustServerCertificate=True;ApplicationIntent=ReadOnly;Application Name=SMC-VocabularyProbe;Connection Timeout=15"
$ds = New-Object System.Data.DataSet
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $sqlText; $cmd.CommandTimeout = 120
    [void](New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($ds)
}
finally { if ($conn.State -ne 'Closed') { $conn.Close() }; $conn.Dispose() }

if ($ds.Tables.Count -ne $names.Count) { throw "Expected $($names.Count) result sets, got $($ds.Tables.Count). Nothing written." }
$out = [ordered]@{ generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o'); database = $Database }
for ($i = 0; $i -lt $names.Count; $i++) { $out[$names[$i]] = ConvertTo-RecordList -Table $ds.Tables[$i] }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$path = Join-Path $OutputDirectory 'application-vocabulary.json'
[System.IO.File]::WriteAllText($path, ($out | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Done. Vocabulary rows: $(@($out['vocabulary']).Count)  Rates: $(@($out['serviceRates']).Count)  Services: $(@($out['services']).Count)" -ForegroundColor Green
Write-Host "Written: $path"
