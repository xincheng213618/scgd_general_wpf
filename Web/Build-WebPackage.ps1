[CmdletBinding()]
param(
    [string]$Revision = 'HEAD',
    [string]$SourceCommit,
    [string]$SourcePath = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) '.artifacts\WebPackages'),
    [string]$PythonRequirementsLock,
    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$python = (Get-Command 'python.exe' -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$git = (Get-Command 'git.exe' -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$npm = (Get-Command 'npm.cmd' -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$builder = Join-Path $PSScriptRoot 'build_web_package.py'
$arguments = @(
    $builder,
    '--git', $git,
    '--npm', $npm,
    "--source-path=$SourcePath",
    "--revision=$Revision",
    "--output-directory=$OutputDirectory"
)
if ($SourceCommit) { $arguments += "--source-commit=$SourceCommit" }
if ($PythonRequirementsLock) { $arguments += "--python-requirements-lock=$PythonRequirementsLock" }
if ($KeepStaging) { $arguments += '--keep-staging' }

$result = & $python @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$result | ConvertFrom-Json
