param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$OpenCvHelperBinary,
    [ValidateCount(4, 4)][int[]]$SearchRegion,
    [ValidateSet("Debug", "Release")][string]$Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$repository = Split-Path -Parent $PSScriptRoot
$previousSource = $env:COLORVISION_RGB_CROSS_SOURCE
$previousOutput = $env:COLORVISION_RGB_CROSS_OUTPUT
$previousRegion = $env:COLORVISION_RGB_CROSS_ROI
$arguments = @('test', 'Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj', '-p:Platform=x64', '-c', $Configuration,
    '--filter', 'FullyQualifiedName~DisplayMetrologyTests', '--logger', 'console;verbosity=minimal')
if ($OpenCvHelperBinary) {
    $binaryPath = (Resolve-Path -LiteralPath $OpenCvHelperBinary).Path
    $arguments += "-p:OpenCvHelperBinary=$binaryPath"
}
Push-Location -LiteralPath $repository
try {
    $env:COLORVISION_RGB_CROSS_SOURCE = $sourcePath
    $env:COLORVISION_RGB_CROSS_OUTPUT = $outputPath
    $env:COLORVISION_RGB_CROSS_ROI = if ($SearchRegion) { $SearchRegion -join "," } else { $null }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "RGB cross validation failed with exit code $LASTEXITCODE." }
    Write-Output "Field measurements and previews: $outputPath"
}
finally {
    $env:COLORVISION_RGB_CROSS_SOURCE = $previousSource
    $env:COLORVISION_RGB_CROSS_OUTPUT = $previousOutput
    $env:COLORVISION_RGB_CROSS_ROI = $previousRegion
    Pop-Location
}
