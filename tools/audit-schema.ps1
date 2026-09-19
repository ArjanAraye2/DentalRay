<#
DentalRay - model vs database schema audit.

Compares every [Table]-mapped model property against the live SQL Server schema,
so a missing migration (like tblStudyPayments.PaymentMethod) is caught before it
reaches a running deployment.

Usage:
  powershell -File tools/audit-schema.ps1 -Server "SERVER\INSTANCE" -User sa -Password "***" -Database DentalRay
#>
param(
    [Parameter(Mandatory = $true)][string]$Server,
    [string]$User,
    [string]$Password,
    [string]$Database = "DentalRay",
    [string]$ModelsPath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ModelsPath) { $ModelsPath = Join-Path $repoRoot "DentalRay.Api/Models" }

# --- read the live schema ---------------------------------------------------
$authArgs = if ($User) { @("-U", $User, "-P", $Password) } else { @("-E") }
$query = "SET NOCOUNT ON; SELECT t.name + '.' + c.name FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id;"
$rows = & sqlcmd -S $Server @authArgs -d $Database -h -1 -Q $query 2>&1
if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $rows" }

$dbCols = @{}
foreach ($row in $rows) {
    $line = $row.Trim()
    if (-not $line) { continue }
    $parts = $line.Split('.', 2)
    if ($parts.Count -ne 2) { continue }
    if (-not $dbCols.ContainsKey($parts[0])) { $dbCols[$parts[0]] = @() }
    $dbCols[$parts[0]] += $parts[1]
}

# --- read the models --------------------------------------------------------
$problems = @()
$checkedTables = 0
foreach ($file in Get-ChildItem $ModelsPath -Filter *.cs) {
    $text = Get-Content $file.FullName -Raw
    $table = [regex]::Match($text, '\[Table\("([^"]+)"\)\]').Groups[1].Value
    if (-not $table) { continue }
    $checkedTables++

    # Every public property with a getter, minus members that EF ignores.
    $props = [regex]::Matches($text, 'public\s+[\w\?<>\.]+\s+(\w+)\s*\{\s*get') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $notMapped = [regex]::Matches($text, '\[NotMapped\][\s\S]{0,200}?public\s+[\w\?<>\.]+\s+(\w+)') |
        ForEach-Object { $_.Groups[1].Value }

    $live = $dbCols[$table]
    if (-not $live) { $problems += "TABLE MISSING IN DB  : $table"; continue }

    foreach ($p in $props) {
        if ($p -in $notMapped) { continue }
        if ($p -notin $live) { $problems += "COLUMN MISSING IN DB : $table.$p" }
    }
}

# --- report -----------------------------------------------------------------
Write-Host "DentalRay schema audit"
Write-Host "  server : $Server"
Write-Host "  database: $Database"
Write-Host "  tables checked: $checkedTables"
Write-Host ""

if ($problems.Count -eq 0) {
    Write-Host "OK - every mapped model property exists in the database." -ForegroundColor Green
    exit 0
}

Write-Host "MISMATCHES FOUND:" -ForegroundColor Red
$problems | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
Write-Host ""
Write-Host "A missing column usually means a migration in DentalRay.Api/Database was never applied." -ForegroundColor Yellow
exit 1
