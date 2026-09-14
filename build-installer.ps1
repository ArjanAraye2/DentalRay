param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $RepoRoot
try {
    $publishRoot = Join-Path $RepoRoot "Publish"
    if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }
    New-Item $publishRoot -ItemType Directory | Out-Null

    dotnet publish ".\DentalRay.Api\DentalRay.Api.csproj" -c $Configuration -r win-x64 --self-contained true -o ".\Publish\DentalRay"
    dotnet publish ".\DentalRay.SetupHelper\DentalRay.SetupHelper.csproj" -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ".\Publish\DentalRay.SetupHelper"

    $databaseDir = ".\Publish\DentalRay.SetupHelper\Database"
    New-Item $databaseDir -ItemType Directory -Force | Out-Null
    Copy-Item ".\DentalRay.SetupHelper\Database\DentalRay.Database.Install.sql" $databaseDir -Force

    $isccCandidates = @(
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    )
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup Compiler (ISCC.exe) was not found." }

    & $iscc ".\Installer\DentalRay.iss"
    Write-Host "DentalRay installer created successfully."
}
finally { Pop-Location }
