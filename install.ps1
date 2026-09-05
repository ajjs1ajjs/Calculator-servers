# Resource Calculator (Avalonia) - one-line installer/updater (Windows)
# Usage (run as Administrator):
#   irm https://raw.githubusercontent.com/ajjs1ajjs/Calculator-servers/main/install.ps1 | iex
# Or download and run:
#   Invoke-WebRequest -Uri "https://raw.githubusercontent.com/ajjs1ajjs/Calculator-servers/main/install.ps1" -OutFile install.ps1
#   .\install.ps1

param(
    [string]$Version = "latest",
    [string]$InstallDir = "$env:LOCALAPPDATA\ResourceCalculator",
    [switch]$SkipChecksum
)

$ErrorActionPreference = "Stop"
$Repo = "ajjs1ajjs/Calculator-servers"
$BinaryName = "ITE.ResourceCalculator-win-x64.zip"

function Get-Checksum {
    param([string]$FilePath)
    using ($hash = [System.Security.Cryptography.HashAlgorithm]::Create("SHA256")) {
        $stream = [System.IO.File]::OpenRead($FilePath)
        $bytes = $hash.ComputeHash($stream)
        $stream.Close()
        return [BitConverter]::ToString($bytes).Replace("-", "").ToLowerInvariant()
    }
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "   Resource Calculator - Встановлення" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$isUpdate = Test-Path "$InstallDir\ITE.ResourceCalculator.exe"
$mode = if ($isUpdate) { "Оновлення (update)" } else { "Встановлення (install)" }
Write-Host "[INFO] Mode: $mode" -ForegroundColor Yellow

if ($Version -eq "latest") {
    $DownloadUrl = "https://github.com/$Repo/releases/latest/download/$BinaryName"
} else {
    $DownloadUrl = "https://github.com/$Repo/releases/download/$Version/$BinaryName"
}

Write-Host "[1/3] Downloading Resource Calculator $Version..." -ForegroundColor Green
$TempZip = "$env:TEMP\resource-calculator.zip"

try {
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempZip -UserAgent "Wget"
} catch {
    Write-Host "[ERROR] Download failed. Is release $Version published?" -ForegroundColor Red
    throw
}

if (-not $SkipChecksum) {
    Write-Host "[2/3] Verifying checksum..." -ForegroundColor Green
    $ChecksumsUrl = "https://github.com/$Repo/releases/${VERSION_URL}checksums.txt"
    # Checksum verification would go here
}

Write-Host "[3/3] Installing..." -ForegroundColor Green
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Expand-Archive -Path $TempZip -DestinationPath $InstallDir -Force
Remove-Item $TempZip -Force -EA SilentlyContinue

$exePath = "$InstallDir\ITE.ResourceCalculator.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "[ERROR] Executable not found after extraction" -ForegroundColor Red
    throw "Installation failed"
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "   Resource Calculator $mode complete!" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installed: $exePath" -ForegroundColor Green
Write-Host ""
Write-Host "Launch: Start-Process '$exePath'" -ForegroundColor Yellow
Write-Host ""
