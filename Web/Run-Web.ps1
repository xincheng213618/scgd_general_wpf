param(
    [int]$Port = 9998,
    [string]$Storage = "",
    [switch]$SkipInstall,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Require-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Run-Checked {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $Command $($Arguments -join ' ')"
    }
}

$WebRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$FrontendRoot = Join-Path $WebRoot "Frontend"
$BackendRoot = Join-Path $WebRoot "Backend"
$Url = "http://127.0.0.1:$Port/"

Require-Command "npm"
Require-Command "python"

Write-Host "ColorVision Web"
Write-Host "Web root: $WebRoot"
Write-Host "URL: $Url"
Write-Host ""

Push-Location $FrontendRoot
try {
    if (-not $SkipInstall -and -not (Test-Path (Join-Path $FrontendRoot "node_modules"))) {
        Write-Host "[1/4] Installing frontend dependencies..."
        Run-Checked "npm" @("install")
    } else {
        Write-Host "[1/4] Frontend dependencies ready."
    }

    if (-not $SkipBuild) {
        Write-Host "[2/4] Building frontend..."
        Run-Checked "npm" @("run", "build")
    } else {
        Write-Host "[2/4] Skipping frontend build."
    }
} finally {
    Pop-Location
}

Push-Location $BackendRoot
try {
    if (-not $SkipInstall) {
        Write-Host "[3/4] Installing backend dependencies..."
        Run-Checked "python" @("-m", "pip", "install", "-r", "requirements.txt")
    } else {
        Write-Host "[3/4] Skipping backend dependency install."
    }

    $BackendArgs = @("app.py", "--port", "$Port")
    if ($Storage.Trim().Length -gt 0) {
        $BackendArgs += @("--storage", $Storage)
    }

    Start-Job -ScriptBlock {
        param([string]$TargetUrl)

        for ($i = 0; $i -lt 45; $i++) {
            try {
                Invoke-WebRequest -UseBasicParsing -Uri $TargetUrl -TimeoutSec 1 | Out-Null
                Start-Process $TargetUrl
                return
            } catch {
                Start-Sleep -Seconds 1
            }
        }

        Start-Process $TargetUrl
    } -ArgumentList $Url | Out-Null

    Write-Host "[4/4] Starting backend..."
    Write-Host "Press Ctrl+C to stop."
    Run-Checked "python" $BackendArgs
} finally {
    Pop-Location
}
