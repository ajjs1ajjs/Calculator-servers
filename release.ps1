<#
.SYNOPSIS
    Публікує новий реліз: перевіряє версію, білдить, тестує, публікує exe та MSI,
    підписує, комітить, тегує й створює GitHub Release.

.DESCRIPTION
    Єдине джерело версії — $(AppVersion) у Directory.Build.props. Скрипт зупиняється,
    якщо тег vX.Y.Z для поточної версії вже існує (локально або на origin) — це і є
    те обов'язкове правило версійності: без бампу AppVersion реліз опублікувати не можна.

.PARAMETER ReleaseNotes
    Текст опису релізу (Markdown). Якщо не передано — візьметься перший розділ CHANGELOG.md.

.EXAMPLE
    ./release.ps1 -ReleaseNotes "Опис змін..."
#>
param(
    [string]$ReleaseNotes
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# --- 1. Версія: читаємо AppVersion з Directory.Build.props ---
[xml]$props = Get-Content "$root/Directory.Build.props"
$version = $props.Project.PropertyGroup.AppVersion
if ([string]::IsNullOrWhiteSpace($version)) { throw "Не вдалося прочитати AppVersion з Directory.Build.props" }
$tag = "v$version"
Write-Host "Версія релізу: $tag" -ForegroundColor Cyan

# --- 2. Обов'язкова перевірка версійності: тег не повинен вже існувати ---
git fetch origin --tags --quiet
$localTag = git tag -l $tag
$remoteTag = git ls-remote --tags origin "refs/tags/$tag"
if ($localTag -or $remoteTag) {
    throw "Тег $tag вже існує (локально або на origin). Забули бампнути AppVersion у Directory.Build.props перед релізом?"
}

# --- 3. Build + тести ---
Write-Host "Білд і тести..." -ForegroundColor Cyan
dotnet build ResourceCalculator.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
dotnet test ResourceCalculator.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

# --- 4. Publish self-contained exe (+ підпис через SignPublishedExe target) ---
Write-Host "Публікація exe..." -ForegroundColor Cyan
dotnet publish ResourceCalculator/ResourceCalculator.csproj -c Release --output publish
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# --- 5. Збірка MSI-інсталятора (класичне оновлення через MajorUpgrade/UpgradeCode) ---
Write-Host "Збірка MSI..." -ForegroundColor Cyan
Push-Location "$root/ResourceCalculator.Installer"
try {
    wix build Package.wxs `
        -d "AppVersion=$version" `
        -d "AppPublishDir=$root/publish/" `
        -d "AppIconPath=$root/ResourceCalculator/icon.ico" `
        -ext WixToolset.UI.wixext `
        -arch x64 `
        -o "$root/publish/ITE.ResourceCalculator.msi"
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }
} finally {
    Pop-Location
}

# --- 6. Git: тег і пуш (коміт — НЕ автоматично: закомітьте зміни вручну перед релізом) ---
$dirty = git status --porcelain
if ($dirty) {
    Write-Host $dirty
    throw "Є незакомічені/невідстежувані зміни (див. вище). Перегляньте їх і закомітьте вручну (git add <конкретні файли>) перед релізом — release.ps1 навмисно не робить це сам, щоб випадково не запушити щось зайве."
}
git push origin master
git tag $tag
git push origin $tag

# --- 7. GitHub Release з обома артефактами ---
if (-not $ReleaseNotes) {
    $changelog = Get-Content "$root/CHANGELOG.md" -Raw
    if ($changelog -match "(?s)^# .*?\n\n(## .*?)\n\n## ") {
        $ReleaseNotes = $Matches[1]
    } else {
        $ReleaseNotes = "Реліз $tag."
    }
}

# --notes-file замість --notes: лапки/спецсимволи в тексті нотаток інакше ламають передачу
# аргументу в gh.exe (PowerShell не екранує вкладені лапки при інтерполяції в native-виклик).
$notesFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($notesFile, $ReleaseNotes, [System.Text.UTF8Encoding]::new($false))
    gh release create $tag `
        "$root/publish/ITE.ResourceCalculator.exe" `
        "$root/publish/ITE.ResourceCalculator.msi" `
        --title "$tag" `
        --notes-file $notesFile
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
} finally {
    Remove-Item $notesFile -Force -ErrorAction SilentlyContinue
}

Write-Host "Готово: реліз $tag опубліковано." -ForegroundColor Green
