param(
    [int]$Port = 9998,
    [string]$Storage = "",
    [switch]$SkipInstall,
    [switch]$SkipBuild,
    [switch]$SkipDocsBuild
)

$ErrorActionPreference = "Stop"
$script:NpmCommand = ""

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

function Test-CommandWorks {
    param(
        [string]$Command,
        [string[]]$Arguments = @("--version")
    )

    try {
        & $Command @Arguments *> $null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Get-NpmCommand {
    if ($script:NpmCommand.Trim().Length -gt 0) {
        return $script:NpmCommand
    }

    $candidates = New-Object System.Collections.Generic.List[string]
    $node = Get-Command "node" -ErrorAction SilentlyContinue
    if ($node -and $node.Source) {
        $nodeDir = Split-Path -Parent $node.Source
        $candidates.Add((Join-Path $nodeDir "npm.cmd"))
    }
    if ($env:ProgramFiles) {
        $candidates.Add((Join-Path $env:ProgramFiles "nodejs\npm.cmd"))
    }
    if (${env:ProgramFiles(x86)}) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "nodejs\npm.cmd"))
    }

    $pathNpm = Get-Command "npm.cmd" -ErrorAction SilentlyContinue
    if ($pathNpm -and $pathNpm.Source) {
        $candidates.Add($pathNpm.Source)
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if ((Test-Path $candidate) -and (Test-CommandWorks $candidate)) {
            $script:NpmCommand = $candidate
            return $script:NpmCommand
        }
    }

    throw "npm cannot be started. Reinstall Node.js LTS or remove the broken npm shim under AppData\Roaming\npm."
}

function Run-FrontendBuild {
    $localBin = Join-Path $FrontendRoot "node_modules\.bin"
    $tsc = Join-Path $localBin "tsc.cmd"
    $vite = Join-Path $localBin "vite.cmd"

    if ((Test-Path $tsc) -and (Test-Path $vite)) {
        Run-Checked $tsc @("-b")
        Run-Checked $vite @("build")
        return
    }

    Run-Checked (Get-NpmCommand) @("run", "build")
}

function Ensure-DocsBuild {
    $docsIndex = Join-Path $RepoRoot "docs\.vitepress\dist\index.html"
    $docsPackage = Join-Path $RepoRoot "package.json"
    if (Test-Path $docsIndex) {
        Write-Host "[3/5] Documentation site ready."
        return
    }
    if (-not (Test-Path $docsPackage)) {
        Write-Host "[3/5] Documentation site skipped: repository package.json was not found."
        return
    }
    if ($SkipDocsBuild) {
        Write-Host "[3/5] Skipping documentation build."
        return
    }

    Push-Location $RepoRoot
    try {
        if (-not $SkipInstall -and -not (Test-Path (Join-Path $RepoRoot "node_modules"))) {
            Write-Host "[3/5] Installing documentation dependencies..."
            Run-Checked (Get-NpmCommand) @("install")
        } else {
            Write-Host "[3/5] Building documentation site..."
        }

        $vitepress = Join-Path $RepoRoot "node_modules\.bin\vitepress.cmd"
        $indexScript = Join-Path $RepoRoot "docs\.vitepress\scripts\generate-docs-index.mjs"
        if (Test-Path $vitepress) {
            Run-Checked $vitepress @("build", "docs")
            if (Test-Path $indexScript) {
                Run-Checked "node" @($indexScript)
            }
        } else {
            Run-Checked (Get-NpmCommand) @("run", "docs:build")
        }
    } finally {
        Pop-Location
    }
}

$WebRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $WebRoot
$FrontendRoot = Join-Path $WebRoot "Frontend"
$BackendRoot = Join-Path $WebRoot "Backend"
$Url = "http://127.0.0.1:$Port/"

Require-Command "node"
Require-Command "python"

Write-Host "ColorVision Web"
Write-Host "Web root: $WebRoot"
Write-Host "URL: $Url"
Write-Host ""

Push-Location $FrontendRoot
try {
    if (-not $SkipInstall -and -not (Test-Path (Join-Path $FrontendRoot "node_modules"))) {
        Write-Host "[1/5] Installing frontend dependencies..."
        Run-Checked (Get-NpmCommand) @("install")
    } else {
        Write-Host "[1/5] Frontend dependencies ready."
    }

    if (-not $SkipBuild) {
        Write-Host "[2/5] Building frontend..."
        Run-FrontendBuild
    } else {
        Write-Host "[2/5] Skipping frontend build."
    }
} finally {
    Pop-Location
}

Ensure-DocsBuild

Push-Location $BackendRoot
try {
    if (-not $SkipInstall) {
        Write-Host "[4/5] Installing backend dependencies..."
        Run-Checked "python" @("-m", "pip", "install", "-r", "requirements.txt")
    } else {
        Write-Host "[4/5] Skipping backend dependency install."
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

    Write-Host "[5/5] Starting backend..."
    Write-Host "Press Ctrl+C to stop."
    Run-Checked "python" $BackendArgs
} finally {
    Pop-Location
}
