<#
Build the independent .NET Framework 4.8 helper locally. No signing service, upload,
installer execution, or changes to the normal main-application release pipeline.
#>
[CmdletBinding()]
param(
    [string]$MSBuildPath,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\.artifacts\ColorVisionSetup\package')
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $MSBuildPath = & $vswhere -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (!$MSBuildPath -or !(Test-Path -LiteralPath $MSBuildPath)) {
    throw 'Visual Studio MSBuild is required. Pass -MSBuildPath and install the .NET Framework 4.8 targeting pack.'
}
$project = Join-Path $repository 'src\ColorVisionSetup\ColorVisionSetup.csproj'
& $MSBuildPath $project /m:1 /nr:false /p:Configuration=Release /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Setup build failed ($LASTEXITCODE)." }

$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$unexpected = @(Get-ChildItem -LiteralPath $output -Force | Where-Object { $_.Name -ne 'ColorVisionSetup.exe' -or $_.PSIsContainer })
if ($unexpected.Count -gt 0) { throw 'Output directory contains other files. Choose an empty output directory; no files were removed.' }
$target = Join-Path $output 'ColorVisionSetup.exe'
Copy-Item -LiteralPath (Join-Path $repository 'src\ColorVisionSetup\bin\x64\Release\ColorVisionSetup.exe') -Destination $target -Force

# Inspect assembly references without invoking the application or requiring .NET 10.
$assembly = [Reflection.Assembly]::LoadFile($target)
$frameworkReferences = @('mscorlib', 'System', 'System.Core', 'System.Net.Http', 'System.Runtime.Serialization', 'System.Xml', 'System.Xaml', 'WindowsBase', 'PresentationCore', 'PresentationFramework')
$external = @($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -notin $frameworkReferences })
if ($external.Count -gt 0) { throw "Unexpected runtime dependencies: $($external.Name -join ', ')" }
if (@($assembly.GetManifestResourceNames() | Where-Object { $_ -match '(?i)\.dll$|logi_' }).Count -gt 0) { throw 'Embedded DLL payloads are not permitted.' }
$file = Get-Item -LiteralPath $target
Write-Output "Single-file helper: $($file.FullName) ($($file.Length) bytes)"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash)"
Write-Output 'Requires Windows x64 with .NET Framework 4.8 or 4.8.1. This local artifact is not Authenticode-signed or uploaded.'
