#Requires -RunAsAdministrator
# Unregister ColorVision Shell Thumbnail Provider
# Must be run as Administrator

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$comHostDll = Join-Path $scriptDir "bin\x64\Debug\net10.0-windows\ColorVision.ShellExtension.comhost.dll"

$handlerClsid = "{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}"
$thumbnailProviderIid = "{E357FCCD-A995-4576-B01F-234630154E96}"

Write-Host "=== ColorVision Shell Thumbnail Provider Unregistration ===" -ForegroundColor Cyan

# Step 1: Remove .cvraw shellex association
Write-Host "[1/4] Removing .cvraw thumbnail handler..." -ForegroundColor Yellow
$cvrawShellexPath = "Registry::HKEY_CLASSES_ROOT\.cvraw\shellex\$thumbnailProviderIid"
if (Test-Path $cvrawShellexPath) {
    Remove-Item -Path $cvrawShellexPath -Force
    Write-Host "  Removed." -ForegroundColor Green
} else {
    Write-Host "  Not found, skipping." -ForegroundColor DarkYellow
}

# Step 2: Remove .cvcie shellex association
Write-Host "[2/4] Removing .cvcie thumbnail handler..." -ForegroundColor Yellow
$cvcieShellexPath = "Registry::HKEY_CLASSES_ROOT\.cvcie\shellex\$thumbnailProviderIid"
if (Test-Path $cvcieShellexPath) {
    Remove-Item -Path $cvcieShellexPath -Force
    Write-Host "  Removed." -ForegroundColor Green
} else {
    Write-Host "  Not found, skipping." -ForegroundColor DarkYellow
}

# Step 3: Remove from approved list
Write-Host "[3/4] Removing from approved shell extensions..." -ForegroundColor Yellow
$approvedPath = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"
if (Test-Path $approvedPath) {
    Remove-ItemProperty -Path $approvedPath -Name $handlerClsid -ErrorAction SilentlyContinue
    Write-Host "  Removed." -ForegroundColor Green
}

# Step 4: Unregister COM server
Write-Host "[4/4] Unregistering COM server..." -ForegroundColor Yellow
if (Test-Path $comHostDll) {
    Start-Process regsvr32 -ArgumentList "/u `"$comHostDll`"" -Wait
    Write-Host "  COM server unregistered." -ForegroundColor Green
} else {
    Write-Host "  comhost.dll not found, skipping." -ForegroundColor DarkYellow
}

# Clear thumbnail cache and restart explorer
Write-Host ""
Write-Host "Clearing thumbnail cache..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db" -Force -ErrorAction SilentlyContinue
Start-Process explorer

Write-Host ""
Write-Host "=== Unregistration complete ===" -ForegroundColor Green
