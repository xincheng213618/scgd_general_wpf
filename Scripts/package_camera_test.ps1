#Requires -Version 7.0
<#
.SYNOPSIS
Build a versioned, clean CameraTest ZIP for manual offline testing. Never uploads.
.EXAMPLE
pwsh -NoProfile -File .\Scripts\package_camera_test.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [switch]$KeepBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent

function Test-CameraTestPackageFile([string]$RelativePath) {
    $path = $RelativePath.Replace('\', '/').ToLowerInvariant()
    if ($path.StartsWith('/') -or $path.Contains(':') -or $path.Split('/') -contains '..') { return $false }
    if ($path.StartsWith('runtimes/') -and !($path.StartsWith('runtimes/win/') -or $path.StartsWith('runtimes/win-x64/'))) { return $false }
    if ($path -match '(^|/)(config|logs?|archive|feedback|test|tests|obj|bin|tools|assets)(/|$)') { return $false }
    if ($path -match '\.(pdb|db|sqlite|sqlite3|lic|log|tmp|bak|cvxp|zip)$') { return $false }
    # Keep all declared managed/vendor DLL dependencies, including camera drivers omitted by Spectrum's filter.
    if ($path.EndsWith('.dll')) { return $true }
    if ($path -in @('cameratest.exe', 'cameratest.deps.json', 'cameratest.runtimeconfig.json', 'cameratest.dll.config')) { return $true }
    # Only repository-supplied SDK defaults; never copy a developer's runtime configuration or licence.
    if ($path -in @('cfg/sys.cfg', 'cfg_files/ikap/510.vlcf', 'cfg_files/mil-dcf/configdcf.ini',
        'cfg_files/mil-dcf/vp101_85mhz_10tap8bit-trigger-good.dcf', 'cfg_files/mil-dcf/vp101_85mhz_4tap12bit-trigger-good.dcf')) { return $true }
    return ($path -match '(^|/)(license|licence|notice|third-party-notices)(\.[a-z0-9_-]+)?\.(txt|md)$')
}

function Assert-CameraTestPayload([string]$Folder) {
    $files = @(Get-ChildItem -LiteralPath $Folder -Recurse -File)
    foreach ($name in @('CameraTest.exe', 'CameraTest.dll', 'CameraTest.deps.json', 'CameraTest.runtimeconfig.json',
        'ColorVision.Engine.dll', 'ColorVision.Core.dll', 'ColorVision.ImageEditor.dll', 'cvColorVision.dll',
        'cvCamera.dll', 'OpenCvSharpExtern.dll', 'opencv_helper.dll', 'sys.cfg')) {
        if (!($files.Name -contains $name)) { throw "Required runtime asset missing: $name" }
    }
    if ($files.Name -contains 'coreclr.dll' -or $files.Name -contains 'System.Private.CoreLib.dll') { throw 'This standard package must not embed the .NET runtime.' }
    $runtime = Get-Content -LiteralPath (Join-Path $Folder 'CameraTest.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (!($runtime.runtimeOptions.frameworks.name -contains 'Microsoft.WindowsDesktop.App')) { throw 'Expected the installed Windows Desktop Runtime dependency.' }
    # Fail closed if cleaning removed an asset selected by the published application's dependency graph.
    $deps = Get-Content -LiteralPath (Join-Path $Folder 'CameraTest.deps.json') -Raw | ConvertFrom-Json
    $target = $deps.targets.PSObject.Properties[$deps.runtimeTarget.name].Value
    foreach ($library in $target.PSObject.Properties) {
        foreach ($kind in @('runtime', 'native', 'resources', 'runtimeTargets')) {
            $assets = $library.Value.PSObject.Properties[$kind]
            if ($null -eq $assets) { continue }
            foreach ($asset in $assets.Value.PSObject.Properties) {
                if ($asset.Name.EndsWith('/_._')) { continue }
                if ($kind -eq 'runtimeTargets' -and $asset.Value.rid -notin @('win', 'win-x64')) { continue }
                $basename = [IO.Path]::GetFileName($asset.Name)
                $candidates = @((Join-Path $Folder $asset.Name), (Join-Path $Folder $basename))
                if ($asset.Value.locale) { $candidates = @((Join-Path $Folder ($asset.Value.locale + '/' + $basename))) }
                if (!($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })) {
                    throw "Published dependency was removed or is missing: $($asset.Name)"
                }
            }
        }
    }
}

function New-CameraTestTestPackage {
    param([string]$Destination, [bool]$PreserveBuild)
    if ([string]::IsNullOrWhiteSpace($Destination)) { $Destination = Join-Path $repositoryRoot 'Release/CameraTest' }
    $Destination = [IO.Path]::GetFullPath($Destination)
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
    $buildRoot = Join-Path $temporaryRoot ('CameraTest-package-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($buildRoot) | Out-Null
    $publishRoot = Join-Path $buildRoot 'publish'
    $buildLog = Join-Path $buildRoot 'build.log'
    $succeeded = $false
    try {
        $arguments = @('publish', (Join-Path $repositoryRoot 'Plugins/CameraTest/CameraTest.csproj'),
            '-c', 'Release', '-f', 'net10.0-windows', '-p:Platform=x64', '-m:1',
            '--self-contained', 'false', '-p:PublishSingleFile=false',
            '-p:PublishTrimmed=false', '-p:GeneratePackageOnBuild=false',
            '--artifacts-path', (Join-Path $buildRoot 'artifacts'), '-o', $publishRoot)
        Write-Host 'Publishing CameraTest to a fresh isolated directory...'
        & dotnet @arguments *> $buildLog
        if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $buildLog -Tail 50; throw "dotnet publish failed ($LASTEXITCODE). Log: $buildLog" }
        $version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $publishRoot 'CameraTest.dll')).FileVersion
        if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Invalid compiled version: $version" }
        $sourceManifest = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Plugins/CameraTest/manifest.json') -Raw | ConvertFrom-Json
        if ($sourceManifest.version -ne $version) { throw 'Source manifest version does not match the compiled CameraTest DLL.' }
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $packageName = "CameraTest-$version-test-win-x64-$stamp"
        $zipPath = Join-Path $Destination ($packageName + '.zip')
        if (Test-Path -LiteralPath $zipPath) { throw "Package already exists; refusing to overwrite: $zipPath" }
        $stageRoot = Join-Path $buildRoot 'stage'
        $packageRoot = Join-Path $stageRoot "CameraTest-$version"
        [IO.Directory]::CreateDirectory($packageRoot) | Out-Null
        $excluded = @()
        foreach ($file in Get-ChildItem -LiteralPath $publishRoot -Recurse -File) {
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link in publish output: $($file.Name)" }
            $relative = [IO.Path]::GetRelativePath($publishRoot, $file.FullName).Replace('\', '/')
            if (!(Test-CameraTestPackageFile $relative)) { $excluded += $relative; continue }
            $target = Join-Path $packageRoot $relative
            [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
        Assert-CameraTestPayload $packageRoot
        @"
相机生产调试 $version — 独立测试版
打包时间：$stamp

使用方法
1. 完整解压到可写目录，双击 CameraTest.exe。不要直接在压缩包中运行，不要只复制 EXE。
2. 无需安装 ColorVision 主程序、数据库或后台服务。电脑需预先配置 .NET 10 Desktop Runtime x64；此包不附带 .NET。
3. 打开 BMP、PNG、JPEG、TIFF 图片后，用下方矩形工具或“框选新增”框住完整 BMW 马蹄靶标。左侧出现区域后点击“开始分析”；移动、删除框后需重新分析。
4. 使用相机时，沿用现有相机驱动与相应 SDK 授权，并在相机设置中选择型号、ID 和匹配的 sys.cfg。
5. 实时分析用于调焦；停止后可记录设备编号/SN、操作员、批次、参数、原图和结果。
6. 默认存档目录为 %LOCALAPPDATA%\ColorVision\CameraTest\Archive，也可在“存档信息”中选择目录。

测试说明
- 本包仅供人工分发测试，不配置在线更新源。
- 相机参数记录为请求 SDK 的值，尚未硬件回读；导入图片的原始拍摄参数未知。
- 判定标准默认留空，应按产品规格设置。缺测、质量警告和未知编码不会显示为合格。
- 真机驱动、取流稳定性和量产重复性需要现场验证。
- 反馈时请附此版本号、相机型号、输入图片/配置及异常现象。备份存档后再更换测试版本。
"@ | Set-Content -LiteralPath (Join-Path $packageRoot '使用说明.txt') -Encoding utf8BOM
        $manifest = [ordered]@{
            product = 'CameraTest'; displayName = '相机生产调试'; version = $version; channel = 'offline-test'
            builtAt = [DateTimeOffset]::Now.ToString('O'); platform = 'win-x64'; selfContained = $false
            onlineUpdate = $false; entryPoint = 'CameraTest.exe'; files = @()
        }
        $manifest.files = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
            [ordered]@{ path = [IO.Path]::GetRelativePath($packageRoot, $_.FullName).Replace('\', '/'); size = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
        $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packageRoot '版本信息.json') -Encoding utf8NoBOM
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::CreateFromDirectory($stageRoot, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
        $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            if ($zip.Entries.Count -ne $manifest.files.Count + 1) { throw 'ZIP inventory differs from the verified payload.' }
            if (!($zip.Entries.FullName -contains "CameraTest-$version/CameraTest.exe")) { throw 'ZIP has no startup executable.' }
        } finally { $zip.Dispose() }
        $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        "$zipHash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath ($zipPath + '.sha256.txt') -Encoding ascii
        Copy-Item -LiteralPath $buildLog -Destination (Join-Path $Destination ($packageName + '.build.log'))
        [ordered]@{ version = $version; file = [IO.Path]::GetFileName($zipPath); size = (Get-Item -LiteralPath $zipPath).Length; sha256 = $zipHash; excluded = $excluded } |
            ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($zipPath + '.report.json') -Encoding utf8NoBOM
        $succeeded = $true
        Write-Host "Created offline test ZIP: $zipPath"
        Write-Host "Version: $version; framework-dependent; files: $($manifest.files.Count + 1)"
        Write-Host "SHA-256: $zipHash"
        return $zipPath
    } finally {
        if ($succeeded -and !$PreserveBuild) {
            # Delete only this invocation's own unique temp directory, after resolving and checking its parent.
            $resolved = (Resolve-Path -LiteralPath $buildRoot).Path.TrimEnd('\', '/')
            if ($resolved -ne $buildRoot -or (Split-Path $resolved -Parent) -ne $temporaryRoot -or
                !(Split-Path $resolved -Leaf).StartsWith('CameraTest-package-') -or
                ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe temporary cleanup target.' }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        } else { Write-Host "Build directory retained: $buildRoot" }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    try { New-CameraTestTestPackage -Destination $OutputDirectory -PreserveBuild $KeepBuild }
    catch { Write-Error $_; exit 1 }
}
