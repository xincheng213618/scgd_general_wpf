[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Root,
    [ValidateRange(1, 65535)][int]$Port = 9998,
    [Parameter(Mandatory)][string]$PythonExe,
    [string]$DataDir,
    [string]$DocsDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-PackageDirectory {
    param([string]$Path, [string]$Name)

    if ($Path -notmatch '^(?:[A-Za-z]:[\\/]|[\\/]{2}[^\\/]+[\\/][^\\/]+(?:[\\/]|\z))') {
        throw "$Name must be an absolute directory."
    }
    $directory = Get-Item -LiteralPath $Path
    if ($directory -isnot [System.IO.DirectoryInfo]) {
        throw "$Name must be a filesystem directory."
    }
    return $directory.FullName
}

$packageRoot = Resolve-PackageDirectory -Path $Root -Name 'Root'
$active = Get-Content -LiteralPath (Join-Path $packageRoot 'active.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($null -eq $active -or $active.schemaVersion -ne 1) {
    throw 'active.json must use schemaVersion 1.'
}
$releaseId = [string]$active.releaseId
$requirementsSha256 = ([string]$active.backendRequirementsSha256).ToLowerInvariant()
if ($releaseId -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z' -or $releaseId.EndsWith('.') -or $requirementsSha256 -cnotmatch '^[0-9a-f]{64}\z') {
    throw 'active.json contains an invalid release ID or backend requirements hash.'
}

$releaseDirectory = Join-Path (Join-Path $packageRoot 'releases') $releaseId
$backendDirectory = Join-Path $releaseDirectory 'Backend'
$appPath = Join-Path $backendDirectory 'app.py'
$requirementsPath = Join-Path $backendDirectory 'requirements-package.txt'
if (-not (Test-Path -LiteralPath $requirementsPath -PathType Leaf)) {
    $requirementsPath = Join-Path $backendDirectory 'requirements.txt'
}
if (-not (Test-Path -LiteralPath $appPath -PathType Leaf) -or -not (Test-Path -LiteralPath $requirementsPath -PathType Leaf)) {
    throw 'The active release is missing Backend/app.py or its Python requirements file.'
}
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$requirementsStream = [System.IO.File]::OpenRead($requirementsPath)
try {
    $actualRequirementsHash = [System.BitConverter]::ToString($sha256.ComputeHash($requirementsStream)).Replace('-', '')
} finally {
    $requirementsStream.Dispose()
    $sha256.Dispose()
}
if ($actualRequirementsHash -ine $requirementsSha256) {
    throw 'The active release requirements do not match active.json.'
}

$dependenciesDirectory = Join-Path (Join-Path $packageRoot 'dependencies') $requirementsSha256
$markerPath = Join-Path $dependenciesDirectory '.colorvision-requirements-sha256'
if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf) -or (Get-Content -LiteralPath $markerPath -Raw -Encoding UTF8).Trim() -ine $requirementsSha256) {
    throw 'The active release dependencies are missing or incomplete.'
}

if ([string]::IsNullOrWhiteSpace($DataDir)) { $DataDir = Join-Path $packageRoot 'data' }
$sharedData = Resolve-PackageDirectory -Path $DataDir -Name 'DataDir'
foreach ($name in @('config.json', 'marketplace.db')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sharedData $name) -PathType Leaf)) {
        throw "The configured data directory is missing $name."
    }
}
$sharedDocs = if ($DocsDir) { Resolve-PackageDirectory -Path $DocsDir -Name 'DocsDir' } else { $null }

$pointerPython = $active.PSObject.Properties['pythonExe']
if ($null -ne $pointerPython -and -not [string]::IsNullOrWhiteSpace([string]$pointerPython.Value)) {
    $PythonExe = [string]$pointerPython.Value
}
$python = if (Test-Path -LiteralPath $PythonExe -PathType Leaf) {
    (Get-Item -LiteralPath $PythonExe).FullName
} else {
    (Get-Command -Name $PythonExe -CommandType Application | Select-Object -First 1).Source
}

$env:COLORVISION_WEB_DATA_DIR = $sharedData
$env:COLORVISION_WEB_DOCS_DIR = $sharedDocs
$env:COLORVISION_WEB_RELEASE_ID = $releaseId
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:PYTHONPATH = if ($env:PYTHONPATH) {
    "$dependenciesDirectory$([System.IO.Path]::PathSeparator)$env:PYTHONPATH"
} else {
    $dependenciesDirectory
}

Write-Output "Starting ColorVision Web release $releaseId on port $Port."
Push-Location -LiteralPath $backendDirectory
try {
    & $python $appPath --port $Port
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $exitCode
