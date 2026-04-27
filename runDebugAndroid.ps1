$ErrorActionPreference = "Stop"

$project = ".\RadioVolna.csproj"
$framework = "net8.0-android"
$apkDir = ".\bin\Debug\net8.0-android"

Write-Host "`n== BUILD ==" -ForegroundColor Cyan
dotnet build $project -f $framework
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

Write-Host "`n== CHECK DEVICE ==" -ForegroundColor Cyan
$adbOutput = adb devices
$adbOutput | ForEach-Object { Write-Host $_ }
$connected = $adbOutput | Select-String "\sdevice$"
if (-not $connected) {
    throw "No authorized Android device found."
}

Write-Host "`n== FIND APK ==" -ForegroundColor Cyan
if (-not (Test-Path $apkDir)) {
    throw "APK directory not found: $apkDir"
}

$apk = Get-ChildItem -Path $apkDir -Filter *.apk -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $apk) {
    throw "No APK file found in $apkDir"
}

Write-Host "Using APK: $($apk.FullName)"

Write-Host "`n== INSTALL ==" -ForegroundColor Cyan
adb install -r "$($apk.FullName)"
if ($LASTEXITCODE -ne 0) {
    throw "APK install failed."
}

Write-Host "`n== DETECT PACKAGE ==" -ForegroundColor Cyan
$pkgCandidates = adb shell pm list packages | ForEach-Object { $_.Trim() }
$package = $null

foreach ($line in $pkgCandidates) {
    if ($line -match '^package:(.+)$') {
        $name = $Matches[1]
        if ($name -eq 'lt.fnkmg.radiovolna') {
            $package = $name
            break
        }
    }
}

if (-not $package) {
    throw "Could not auto-detect package name after install."
}

Write-Host "Using package: $package"

Write-Host "`n== LAUNCH ==" -ForegroundColor Cyan
adb shell am force-stop $package
adb shell monkey -p $package -c android.intent.category.LAUNCHER 1
if ($LASTEXITCODE -ne 0) {
    throw "App launch failed."
}

Write-Host "`nDone." -ForegroundColor Green