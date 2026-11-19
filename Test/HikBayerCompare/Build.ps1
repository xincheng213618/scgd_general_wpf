# Run from a Visual Studio x64 Developer PowerShell with the MVS development headers installed.
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../artifacts/HikBayerCompare'),
    [string]$MvsInclude = "${env:ProgramFiles(x86)}\MVS\Development\Includes"
)
$ErrorActionPreference = 'Stop'
$target = [System.IO.Directory]::CreateDirectory($OutputDirectory).FullName
$program = Join-Path $target 'HikBayerCompare.exe'
& cl.exe /nologo /std:c++17 /O2 /MT /EHsc /utf-8 "/I$MvsInclude" "/Fo$target\HikBayerCompare.obj" "/Fe$program" (Join-Path $PSScriptRoot 'HikBayerCompare.cpp')
if ($LASTEXITCODE -ne 0) { throw "HikBayerCompare compilation failed ($LASTEXITCODE)." }
Write-Output $program
