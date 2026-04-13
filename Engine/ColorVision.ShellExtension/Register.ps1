#Requires -RunAsAdministrator
# Register ColorVision Shell Thumbnail Provider
# Must be run as Administrator

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$comHostDll = Join-Path $scriptDir "bin\x64\Debug\net10.0-windows\ColorVision.ShellExtension.comhost.dll"

if (-not (Test-Path $comHostDll)) {
    # Also try Release path
    $comHostDll = Join-Path $scriptDir "bin\x64\Release\net10.0-windows\ColorVision.ShellExtension.comhost.dll"
    if (-not (Test-Path $comHostDll)) {
        Write-Error "comhost.dll not found. Build the project first."
        exit 1
    }
}

$handlerClsid = "{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}"
$thumbnailProviderIid = "{E357FCCD-A995-4576-B01F-234630154E96}"

Write-Host "=== ColorVision Shell Thumbnail Provider Registration ===" -ForegroundColor Cyan

# Step 1: Register COM server via regsvr32
Write-Host "[1/4] Registering COM server..." -ForegroundColor Yellow
$regResult = Start-Process regsvr32 -ArgumentList "`"$comHostDll`"" -Wait -PassThru
if ($regResult.ExitCode -ne 0) {
    Write-Error "regsvr32 failed with exit code $($regResult.ExitCode)"
    exit 1
}
Write-Host "  COM server registered." -ForegroundColor Green

# Step 2: Associate .cvraw with the thumbnail handler
Write-Host "[2/4] Registering .cvraw thumbnail handler..." -ForegroundColor Yellow
$cvrawShellexPath = "Registry::HKEY_CLASSES_ROOT\.cvraw\shellex\$thumbnailProviderIid"
New-Item -Path $cvrawShellexPath -Force | Out-Null
Set-ItemProperty -Path $cvrawShellexPath -Name "(Default)" -Value $handlerClsid
Write-Host "  .cvraw handler registered." -ForegroundColor Green

# Step 3: Associate .cvcie with the thumbnail handler
Write-Host "[3/4] Registering .cvcie thumbnail handler..." -ForegroundColor Yellow
$cvcieShellexPath = "Registry::HKEY_CLASSES_ROOT\.cvcie\shellex\$thumbnailProviderIid"
New-Item -Path $cvcieShellexPath -Force | Out-Null
Set-ItemProperty -Path $cvcieShellexPath -Name "(Default)" -Value $handlerClsid
Write-Host "  .cvcie handler registered." -ForegroundColor Green

# Step 4: Add to approved shell extensions list (required on some Windows versions)
Write-Host "[4/4] Adding to approved shell extensions..." -ForegroundColor Yellow
$approvedPath = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
if (Test-Path $approvedPath) {
    Set-ItemProperty -Path $approvedPath -Name $handlerClsid -Value "ColorVision Thumbnail Handler"
    Write-Host "  Added to approved list." -ForegroundColor Green
} else {
    Write-Host "  Approved list path not found, skipping (may not be required)." -ForegroundColor DarkYellow
}

# Clear thumbnail cache
Write-Host ""
Write-Host "Clearing thumbnail cache..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db" -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\iconcache_*.db" -Force -ErrorAction SilentlyContinue
Start-Process explorer

Write-Host ""
Write-Host "=== Registration complete ===" -ForegroundColor Green
Write-Host "Explorer has been restarted. Thumbnail cache cleared."
Write-Host "Navigate to a folder with .cvraw/.cvcie files to test."
