$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:JAVA_HOME = "C:\Program Files\Android\openjdk\jdk-21.0.8"
$env:ANDROID_HOME = Join-Path $projectRoot ".android-sdk"
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME

$gradle = Join-Path $projectRoot ".tools\gradle-8.9\bin\gradle.bat"
$keytool = Join-Path $env:JAVA_HOME "bin\keytool.exe"
$signingDir = Join-Path $projectRoot ".signing"
$keystorePath = Join-Path $signingDir "colorvision-release.jks"
$signingPropertiesPath = Join-Path $projectRoot "signing.properties"
$keyAlias = "colorvision-release"

function New-Password {
    $bytes = New-Object byte[] 24
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', 'A').Replace('/', 'B')
}

function Ensure-ReleaseSigning {
    if ((Test-Path $keystorePath) -and (Test-Path $signingPropertiesPath)) {
        Write-Host "[1/3] Release signing ready."
        return
    }

    if (-not (Test-Path $keytool)) {
        throw "keytool was not found: $keytool"
    }

    New-Item -ItemType Directory -Force -Path $signingDir | Out-Null
    $storePassword = New-Password
    $keyPassword = $storePassword

    Write-Host "[1/3] Creating local release signing key..."
    & $keytool `
        -genkeypair `
        -v `
        -keystore $keystorePath `
        -storetype JKS `
        -storepass $storePassword `
        -keypass $keyPassword `
        -alias $keyAlias `
        -keyalg RSA `
        -keysize 2048 `
        -validity 10000 `
        -dname "CN=ColorVision, OU=ColorVision, O=ColorVision, L=Shenzhen, ST=Guangdong, C=CN"

    if ($LASTEXITCODE -ne 0) {
        throw "keytool failed with exit code $LASTEXITCODE"
    }

    $properties = @(
        "storeFile=.signing/colorvision-release.jks"
        "storePassword=$storePassword"
        "keyAlias=$keyAlias"
        "keyPassword=$keyPassword"
    )
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($signingPropertiesPath, $properties, $utf8NoBom)
}

Ensure-ReleaseSigning

Push-Location $projectRoot
try {
    Write-Host "[2/3] Building release APK..."
    & $gradle --no-daemon assembleRelease
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

$apkPath = Join-Path $projectRoot "app\build\outputs\apk\release\app-release.apk"
if (-not (Test-Path $apkPath)) {
    throw "Release APK was not created: $apkPath"
}

Write-Host "[3/3] Release APK ready."
Write-Host ""
Write-Host "APK:"
Write-Host $apkPath
