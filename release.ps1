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
Write-Host "Публікація exe (WPF, Windows)..." -ForegroundColor Cyan
dotnet publish ResourceCalculator/ResourceCalculator.csproj -c Release --output publish
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# --- 4b. Publish Avalonia (крос-платформна версія для Ubuntu / macOS) ---
Write-Host "Публікація Avalonia (Linux / macOS)..." -ForegroundColor Cyan
$avaloniaProject = "ResourceCalculator.Avalonia/ResourceCalculator.Avalonia.csproj"
$avaloniaRids = @("linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
foreach ($rid in $avaloniaRids) {
    Write-Host "  -> $rid ..." -ForegroundColor DarkCyan
    $outDir = "$root/publish/avalonia-$rid"
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    dotnet publish $avaloniaProject -c Release -r $rid --output $outDir
    if ($LASTEXITCODE -ne 0) { throw "Publish Avalonia $rid failed" }
}

# --- 4c. Пакування Linux/macOS артефактів ---
Write-Host "Пакування Linux/macOS..." -ForegroundColor Cyan

# Linux: tar.gz з бінарником + .desktop + іконкою
foreach ($rid in @("linux-x64", "linux-arm64")) {
    $srcDir = "$root/publish/avalonia-$rid"
    $binary = Join-Path $srcDir "ITE.ResourceCalculator"
    if (-not (Test-Path $binary)) { $binary = Join-Path $srcDir "ITE.ResourceCalculator.exe" }
    if (-not (Test-Path $binary)) { throw "Не знайдено бінарник для $rid у $srcDir" }
    $stage = "$root/publish/stage-$rid"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage | Out-Null
    Copy-Item $binary (Join-Path $stage "ITE.ResourceCalculator") -Force
    # .desktop поруч для ручного встановлення
    Copy-Item "$root/ResourceCalculator.Avalonia/packaging/linux/ite-resource-calculator.desktop" $stage -Force -ErrorAction SilentlyContinue
    # іконка (опційно)
    Copy-Item "$root/ResourceCalculator/icon.ico" (Join-Path $stage "icon.ico") -Force -ErrorAction SilentlyContinue
    $tarName = "ITE.ResourceCalculator-$rid.tar.gz"
    $tarPath = "$root/publish/$tarName"
    if (Test-Path $tarPath) { Remove-Item $tarPath -Force }
    Push-Location $stage
    try {
        # Використовуємо tar (вбудований у Windows 10+ / Git Bash)
        & tar -czf $tarPath *
        if ($LASTEXITCODE -ne 0) { throw "tar $rid failed" }
    } finally { Pop-Location }
    Write-Host "    створено $tarName" -ForegroundColor Green
}

# macOS: .app bundle + zip
foreach ($rid in @("osx-x64", "osx-arm64")) {
    $srcDir = "$root/publish/avalonia-$rid"
    $binary = Join-Path $srcDir "ITE.ResourceCalculator"
    if (-not (Test-Path $binary)) { $binary = Join-Path $srcDir "ITE.ResourceCalculator.exe" }
    if (-not (Test-Path $binary)) { throw "Не знайдено бінарник для $rid у $srcDir" }
    $appName = "ITE.ResourceCalculator.app"
    $appRoot = "$root/publish/stage-$rid/$appName"
    if (Test-Path "$root/publish/stage-$rid") { Remove-Item "$root/publish/stage-$rid" -Recurse -Force }
    New-Item -ItemType Directory -Path "$appRoot/Contents/MacOS" -Force | Out-Null
    New-Item -ItemType Directory -Path "$appRoot/Contents/Resources" -Force | Out-Null
    Copy-Item $binary "$appRoot/Contents/MacOS/ITE.ResourceCalculator" -Force
    $plistSrc = "$root/ResourceCalculator.Avalonia/packaging/macos/Info.plist"
    $plistDst = "$appRoot/Contents/Info.plist"
    if (Test-Path $plistSrc) {
        (Get-Content $plistSrc -Raw).Replace("__APP_VERSION__", $version) | Set-Content $plistDst -Encoding UTF8
    }
    Copy-Item "$root/ResourceCalculator/icon.ico" "$appRoot/Contents/Resources/AppIcon.ico" -Force -ErrorAction SilentlyContinue
    $zipName = "ITE.ResourceCalculator-$rid.zip"
    $zipPath = "$root/publish/$zipName"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Push-Location "$root/publish/stage-$rid"
    try {
        Compress-Archive -Path $appName -DestinationPath $zipPath -Force
    } finally { Pop-Location }
    Write-Host "    створено $zipName" -ForegroundColor Green
}

# Додатково: Avalonia для Windows (для користувачів, які хочуть крос-платформний UI на Windows)
Write-Host "Пакування Avalonia Windows..." -ForegroundColor Cyan
$winSrc = "$root/publish/avalonia-win-x64"
if (-not (Test-Path $winSrc)) {
    dotnet publish $avaloniaProject -c Release -r win-x64 --output $winSrc
    if ($LASTEXITCODE -ne 0) { throw "Publish Avalonia win-x64 failed" }
}
$winZip = "$root/publish/ITE.ResourceCalculator-avalonia-win-x64.zip"
if (Test-Path $winZip) { Remove-Item $winZip -Force }
Compress-Archive -Path "$winSrc/*" -DestinationPath $winZip -Force
Write-Host "    створено ITE.ResourceCalculator-avalonia-win-x64.zip" -ForegroundColor Green

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

# --- 7. GitHub Release з усіма артефактами (Windows + Linux + macOS) ---
if (-not $ReleaseNotes) {
    $changelog = Get-Content "$root/CHANGELOG.md" -Raw
    if ($changelog -match "(?s)^# .*?\n\n(## .*?)\n\n## ") {
        $ReleaseNotes = $Matches[1]
    } else {
        $ReleaseNotes = "Реліз $tag."
    }
}

$releaseAssets = @(
    "$root/publish/ITE.ResourceCalculator.exe"
    "$root/publish/ITE.ResourceCalculator.msi"
    "$root/publish/ITE.ResourceCalculator-linux-x64.tar.gz"
    "$root/publish/ITE.ResourceCalculator-linux-arm64.tar.gz"
    "$root/publish/ITE.ResourceCalculator-osx-x64.zip"
    "$root/publish/ITE.ResourceCalculator-osx-arm64.zip"
    "$root/publish/ITE.ResourceCalculator-avalonia-win-x64.zip"
) | Where-Object { Test-Path $_ }

if ($releaseAssets.Count -lt 2) { throw "Не знайдено артефактів для релізу: $releaseAssets" }
Write-Host "Артефакти релізу:" -ForegroundColor Cyan
$releaseAssets | ForEach-Object { Write-Host "  $_" }

# --notes-file замість --notes: лапки/спецсимволи в тексті нотаток інакше ламають передачу
# аргументу в gh.exe (PowerShell не екранує вкладені лапки при інтерполяції в native-виклик).
$notesFile = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($notesFile, $ReleaseNotes, [System.Text.UTF8Encoding]::new($false))
    gh release create $tag @releaseAssets --title "$tag" --notes-file $notesFile
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
} finally {
    Remove-Item $notesFile -Force -ErrorAction SilentlyContinue
}

Write-Host "Готово: реліз $tag опубліковано." -ForegroundColor Green
