$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:JAVA_HOME = "C:\Program Files\Android\openjdk\jdk-21.0.8"
$env:ANDROID_HOME = Join-Path $projectRoot ".android-sdk"
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME

Push-Location $projectRoot
try {
    & (Join-Path $projectRoot ".tools\gradle-8.9\bin\gradle.bat") --no-daemon assembleDebug
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "APK:"
Write-Host (Join-Path $projectRoot "app\build\outputs\apk\debug\app-debug.apk")
