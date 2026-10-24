param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdkRoot = Join-Path $repoRoot 'SDK\ColorVision.FlowPoi.Client.Net48'
$demoProject = Join-Path $sdkRoot 'Demo\FlowPoiClientDemo.csproj'
[xml]$project = [IO.File]::ReadAllText($demoProject)
$version = [string]$project.Project.PropertyGroup.VersionPrefix
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Demo VersionPrefix is missing.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\FlowPoiClientDemo'
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$packageName = "FlowPoiClientDemo-net48-x64-$version"
$stagePath = Join-Path $outputRoot ($packageName + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$zipPath = Join-Path $outputRoot ($packageName + '.zip')
if (Test-Path -LiteralPath $zipPath) { throw "Package already exists: $zipPath. Select another OutputDirectory." }

dotnet build $demoProject -c Release -p:Platform=x64 --nologo -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Demo build failed.' }

$binarySource = Join-Path $sdkRoot 'Demo\bin\x64\Release\net48'
$binaryTarget = Join-Path $stagePath 'Binaries'
$sourceTarget = Join-Path $stagePath 'Source'
New-Item -ItemType Directory -Path $binaryTarget -Force | Out-Null
New-Item -ItemType Directory -Path $sourceTarget -Force | Out-Null
foreach ($name in @('FlowPoiClientDemo.exe', 'FlowPoiClientDemo.exe.config', 'ColorVision.FlowPoi.Client.Net48.dll', 'Newtonsoft.Json.dll')) {
    Copy-Item -LiteralPath (Join-Path $binarySource $name) -Destination $binaryTarget
}
New-Item -ItemType Directory -Path (Join-Path $binaryTarget 'Samples') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sdkRoot 'Samples\flow-poi-response.json') -Destination (Join-Path $binaryTarget 'Samples')
foreach ($relative in @(
    'ColorVision.FlowPoi.Client.Net48.csproj', 'FlowPoiClient.cs', 'FlowPoiResponse.cs', 'README.md', 'API.md',
    'Samples\flow-poi-response.json', 'ThirdPartyNotices\Newtonsoft.Json.LICENSE.md',
    'Demo\FlowPoiClientDemo.csproj', 'Demo\Program.cs',
    'Demo\DemoResultFiles.cs', 'Demo\MainWindow.xaml', 'Demo\MainWindow.xaml.cs'
)) {
    $target = Join-Path $sourceTarget $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sdkRoot $relative) -Destination $target
}
Copy-Item -LiteralPath (Join-Path $sdkRoot 'README.md') -Destination $stagePath
Copy-Item -LiteralPath (Join-Path $sdkRoot 'API.md') -Destination $stagePath
New-Item -ItemType Directory -Path (Join-Path $stagePath 'Samples') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sdkRoot 'Samples\flow-poi-response.json') -Destination (Join-Path $stagePath 'Samples')
New-Item -ItemType Directory -Path (Join-Path $stagePath 'ThirdPartyNotices') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sdkRoot 'ThirdPartyNotices\Newtonsoft.Json.LICENSE.md') -Destination (Join-Path $stagePath 'ThirdPartyNotices')
Compress-Archive -LiteralPath @(
    $binaryTarget, $sourceTarget, (Join-Path $stagePath 'README.md'),
    (Join-Path $stagePath 'API.md'), (Join-Path $stagePath 'Samples'), (Join-Path $stagePath 'ThirdPartyNotices')
) -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output "Package: $zipPath"
Write-Output "Contents: $stagePath"
Write-Output 'Runtime: .NET Framework 4.8 / Windows x64; no .NET Core runtime or ColorVision client-side assemblies.'
