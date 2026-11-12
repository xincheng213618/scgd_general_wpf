<#
Build the standalone .NET Framework 4.8 update relay tool locally. No signing service, upload,
or changes to the normal main-application release pipeline.
#>
[CmdletBinding()]
param(
    [string]$MSBuildPath,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\.artifacts\ColorVisionUpdateRelay\package')
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
$project = Join-Path $repository 'src\ColorVisionUpdateRelay\ColorVisionUpdateRelay.csproj'
& $MSBuildPath $project /m:1 /nr:false /p:Configuration=Release /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Update relay build failed ($LASTEXITCODE)." }

$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$unexpected = @(Get-ChildItem -LiteralPath $output -Force | Where-Object { $_.Name -ne 'ColorVisionUpdateRelay.exe' -or $_.PSIsContainer })
if ($unexpected.Count -gt 0) { throw 'Output directory contains other files. Choose an empty output directory; no files were removed.' }
$target = Join-Path $output 'ColorVisionUpdateRelay.exe'
Copy-Item -LiteralPath (Join-Path $repository 'src\ColorVisionUpdateRelay\bin\x64\Release\ColorVisionUpdateRelay.exe') -Destination $target -Force

# Inspect assembly references without running the tool (it requires administrator rights).
$assembly = [Reflection.Assembly]::LoadFile($target)
$frameworkReferences = @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.ServiceProcess', 'System.Windows.Forms')
$external = @($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -notin $frameworkReferences })
if ($external.Count -gt 0) { throw "Unexpected runtime dependencies: $($external.Name -join ', ')" }
$file = Get-Item -LiteralPath $target
Write-Output "Single-file tool: $($file.FullName) ($($file.Length) bytes)"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash)"
Write-Output 'Requires Windows x64 with .NET Framework 4.8 or 4.8.1 and administrator rights. This local artifact is not Authenticode-signed or uploaded.'
