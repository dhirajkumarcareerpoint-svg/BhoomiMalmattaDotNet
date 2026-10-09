<#
.SYNOPSIS
  Starts SMC Bhoomi Malmatta locally with IIS Express. READ-ONLY with respect to the database schema:
  it never creates, alters, drops or migrates anything. The application itself only reads/writes ordinary data rows.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File .\run-local.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File .\run-local.ps1 -Build   # restore + build first (needs Visual Studio 2022 / Build Tools)
#>
[CmdletBinding()]
param([switch]$Build, [int]$Port = 58080)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$web  = Join-Path $root 'SMC.BhoomiMalmatta.Web'
$cfg  = Join-Path $web 'Web.config'
$sln  = Join-Path $root 'SMC.BhoomiMalmatta.sln'

# 1) Database safety: the configured database must be exactly SMC_BhoomiMalmatta
[xml]$xml = Get-Content -LiteralPath $cfg -Raw -Encoding UTF8
$cs = ($xml.configuration.connectionStrings.add | Where-Object { $_.name -eq 'SmcDatabase' }).connectionString
if (-not $cs) { throw "Connection string 'SmcDatabase' not found in Web.config." }
Add-Type -AssemblyName System.Data
$b  = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
$db = $b.InitialCatalog
if ($db -cne 'SMC_BhoomiMalmatta') { throw "Refusing to start: database must be exactly 'SMC_BhoomiMalmatta' (found '$db')." }
if ($b.AttachDBFilename -or $b.UserInstance) { throw "Refusing to start: attaching database files / user instances is not allowed." }
Write-Host "Database check OK: $db (Windows Authentication, no schema operations)." -ForegroundColor Green

# 2) IIS Express
$iis = @("$env:ProgramFiles\IIS Express\iisexpress.exe", "${env:ProgramFiles(x86)}\IIS Express\iisexpress.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iis) { throw "IIS Express not found. Install Visual Studio 2022 (ASP.NET workload) or IIS Express." }

# 3) Optional build (restore + compile). MSBuild comes from Visual Studio 2022.
if ($Build -or -not (Test-Path (Join-Path $web 'bin\SMC.BhoomiMalmatta.Web.dll'))) {
  $vsw = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
  if (-not (Test-Path $vsw)) { throw "Visual Studio 2022 not found (vswhere.exe missing). Open the solution in Visual Studio and build once, or install VS 2022." }
  $msbuild = & $vsw -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
  if (-not $msbuild) { throw "MSBuild.exe not found." }
  Write-Host "Building with $msbuild ..."
  & $msbuild $sln /restore /p:Configuration=Debug /v:minimal
  if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

# 4) Start
$url = "http://localhost:$Port/"
Write-Host "Starting IIS Express. Local URL: $url   (Ctrl+C to stop)" -ForegroundColor Cyan
& $iis "/path:$web" "/port:$Port"
