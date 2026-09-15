#Requires -Version 5.1
<#
.SYNOPSIS
  Sync docs with code (counters, version, stamps, API-drift detection).
  Update mode (default): rewrites AUTO-markers in MD docs.
  Check mode (-Check): exits 1 with drift list, writes nothing. Used by
  pre-commit hook (.githooks) and CI (docs-sync.yml).
.EXAMPLE
  powershell -File scripts/Update-Docs.ps1
  powershell -File scripts/Update-Docs.ps1 -Check
#>
param(
    [switch]$Check,
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
)

$ErrorActionPreference = 'Stop'
$failures = @()

function Get-Marker([string]$text, [string]$name) {
    $m = [regex]::Match($text, "<!-- AUTO:$name -->(.*?)<!-- /AUTO -->",
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if ($m.Success) { return $m.Groups[1].Value.Trim() }
    return $null
}

function Set-Marker([string]$text, [string]$name, [string]$val) {
    return [regex]::Replace($text, "<!-- AUTO:$name -->.*?<!-- /AUTO -->",
        "<!-- AUTO:$name -->$val<!-- /AUTO -->",
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
}

function Read-File([string]$rel) {
    return [System.IO.File]::ReadAllText((Join-Path $Root $rel), [System.Text.Encoding]::UTF8)
}

function Write-File([string]$rel, [string]$text) {
    [System.IO.File]::WriteAllText((Join-Path $Root $rel), $text, (New-Object System.Text.UTF8Encoding $false))
}

# --- Facts from code ---
$head = (git -C $Root rev-parse --short HEAD).Trim()
$date = (git -C $Root log -1 --format=%ad --date=short).Trim()
$props = Read-File 'Directory.Build.props'
$appVersion = [regex]::Match($props, '<AppVersion>([^<]+)</AppVersion>').Groups[1].Value.Trim()

$testFiles = Join-Path $Root 'ResourceCalculator.Tests/*.cs'
$facts = @(Select-String -Path $testFiles -Pattern '\[Fact\]').Count
$inline = @(Select-String -Path $testFiles -Pattern '\[InlineData').Count
$testTotal = $facts + $inline  # theory cases counted via InlineData

$arch = Read-File 'ARCHITECTURE.md'
$func = Read-File 'FUNCTIONS.md'
$agents = Read-File 'AGENTS.md'
$testsDoc = Read-File 'TESTS.md'
$updateSvc = Read-File 'ResourceCalculator.Core/Interfaces/IUpdateCheckService.cs'
$exportSvc = Read-File 'ResourceCalculator.Core/Services/ConfigExportService.cs'

# --- Content-drift checks ---
# 1. ARCHITECTURE must not describe WPF (Avalonia migration done in c98f90b)
$wpfPatterns = @('WpfDialogService', 'WpfThemeService', 'WPF UI', 'WPF-')
foreach ($pat in $wpfPatterns) {
    if ($arch.Contains($pat)) { $failures += "ARCHITECTURE.md has stale '$pat' (expected Avalonia)" }
}

# 2. Code <-> docs symmetry for UpdateInfo fields
foreach ($field in @('ReleaseNotes', 'SizeBytes')) {
    $inCode = $updateSvc.Contains($field)
    $inDoc = $func.Contains($field)
    if ($inCode -and -not $inDoc) { $failures += "FUNCTIONS.md misses UpdateInfo.$field (present in IUpdateCheckService.cs)" }
    if ($inDoc -and -not $inCode) { $failures += "FUNCTIONS.md mentions $field (gone from IUpdateCheckService.cs)" }
}
if ($exportSvc.Contains('ExportExcel(ResourceRequirement req, ProjectConfig config') -and
    -not $func.Contains('ExportExcel(ResourceRequirement req, ProjectConfig config')) {
    $failures += 'FUNCTIONS.md has old ExportExcel param order (must be req, config)'
}
foreach ($m in @('EnsureUnlockedAsync', 'AddRowCommand')) {
    if (-not $func.Contains($m)) { $failures += "FUNCTIONS.md misses '$m' (present in MatrixViewModel)" }
}

# 3. Numeric markers match code
$v = Get-Marker $testsDoc 'tests-total'
if (($v -ne $null) -and ($v -ne "$testTotal")) { $failures += "TESTS.md tests-total=$v, code has $testTotal" }
$v = Get-Marker $agents 'tests-total'
if (($v -ne $null) -and ($v -ne "$testTotal")) { $failures += "AGENTS.md tests-total=$v, code has $testTotal" }
$av = Get-Marker $agents 'app-version'
if (($av -ne $null) -and ($av -ne $appVersion)) { $failures += "AGENTS.md app-version=$av, Directory.Build.props has $appVersion" }

if ($Check) {
    if ($failures.Count -gt 0) {
        Write-Output 'DOCS-STALE:'
        foreach ($f in $failures) { Write-Output "  - $f" }
        Write-Output 'Run: powershell -File scripts/Update-Docs.ps1 (then add content changes manually)'
        exit 1
    }
    Write-Output "docs-ok (tests: $testTotal, version: $appVersion, HEAD: $head)"
    exit 0
}

# --- Update mode: rewrite markers ---
if ($failures.Count -gt 0) {
    Write-Output 'NOTE: content drift (markers updated, but fix content manually):'
    foreach ($f in $failures) { Write-Output "  - $f" }
}

$stamp = "Verified: $date, commit ``$head`` (scripts/Update-Docs.ps1)"
foreach ($doc in @('ARCHITECTURE.md', 'DATA-MODELS.md', 'IMPLEMENTATION.md', 'FUNCTIONS.md', 'TESTS.md')) {
    $t = Read-File $doc
    if ($t.Contains('<!-- AUTO:stamp -->')) {
        $t = Set-Marker $t 'stamp' $stamp
        Write-File $doc $t
        Write-Output "stamp: $doc"
    }
}

$testsDoc = Read-File 'TESTS.md'
$testsDoc = Set-Marker $testsDoc 'tests-total' "$testTotal"
Write-File 'TESTS.md' $testsDoc

$agents = Read-File 'AGENTS.md'
$agents = Set-Marker $agents 'tests-total' "$testTotal"
$agents = Set-Marker $agents 'app-version' $appVersion
foreach ($n in @('arch', 'data', 'impl', 'func', 'tests')) {
    $agents = Set-Marker $agents "$n-commit" "``$head``"
    $agents = Set-Marker $agents "$n-date" $date
}
Write-File 'AGENTS.md' $agents
Write-Output "AGENTS.md: tests=$testTotal version=$appVersion HEAD=$head"
