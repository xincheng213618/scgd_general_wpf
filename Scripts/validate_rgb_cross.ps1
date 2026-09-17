param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$OpenCvHelperBinary
)

$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$repository = Split-Path -Parent $PSScriptRoot
$previousSource = $env:COLORVISION_RGB_CROSS_SOURCE
$previousOutput = $env:COLORVISION_RGB_CROSS_OUTPUT
$arguments = @('test', 'Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj', '-p:Platform=x64',
    '--filter', 'FullyQualifiedName~DisplayMetrologyTests', '--logger', 'console;verbosity=minimal')
if ($OpenCvHelperBinary) {
    $binaryPath = (Resolve-Path -LiteralPath $OpenCvHelperBinary).Path
    $arguments += "-p:OpenCvHelperBinary=$binaryPath"
}
Push-Location -LiteralPath $repository
try {
    $env:COLORVISION_RGB_CROSS_SOURCE = $sourcePath
    $env:COLORVISION_RGB_CROSS_OUTPUT = $outputPath
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "RGB cross validation failed with exit code $LASTEXITCODE." }
    Write-Output "Field measurements and previews: $outputPath"
}
finally {
    $env:COLORVISION_RGB_CROSS_SOURCE = $previousSource
    $env:COLORVISION_RGB_CROSS_OUTPUT = $previousOutput
    Pop-Location
}
