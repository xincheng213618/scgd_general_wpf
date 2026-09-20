#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../package_camera_test.ps1')

foreach ($path in @('CameraTest.exe', 'runtimes/win-x64/native/cvCamera.dll', 'toupcam.dll', 'nncam.dll',
    'IKapC.dll', 'cfg/sys.cfg', 'zh-Hans/ColorVision.UI.resources.dll', 'LICENSE.txt')) {
    if (!(Test-CameraTestPackageFile $path)) { throw "Required distribution asset was excluded: $path" }
}
foreach ($path in @('ColorVision.Engine.exe', 'ColorVision.UI.Desktop.runtimeconfig.json', 'Config/CameraTest.json',
    'Archive/SN/image.png', 'Logs/log.txt', 'camera.lic', 'ColorVision.Database.pdb', 'test.db', 'profile.json',
    'runtimes/win-arm64/native/cvCamera.dll', 'runtimes/linux-x64/native/lib.so', 'Tools/Spectrum/zadig.exe',
    'Assets/Tool/aria2c.exe', '../unrelated.dll', 'C:/private.dll')) {
    if (Test-CameraTestPackageFile $path) { throw "Unexpected file accepted: $path" }
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('CameraTest-package-contract-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
try {
    foreach ($name in @('CameraTest.exe', 'CameraTest.dll', 'ColorVision.Engine.dll', 'ColorVision.Core.dll',
        'ColorVision.ImageEditor.dll', 'cvColorVision.dll', 'cvCamera.dll', 'OpenCvSharpExtern.dll', 'opencv_helper.dll', 'sys.cfg')) {
        [IO.File]::WriteAllBytes((Join-Path $testRoot $name), [byte[]]@(0))
    }
    '{"runtimeOptions":{"frameworks":[{"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}]}}' |
        Set-Content -LiteralPath (Join-Path $testRoot 'CameraTest.runtimeconfig.json')
    '{"runtimeTarget":{"name":"test"},"targets":{"test":{"Example/1.0":{"runtime":{"lib/net10.0/MissingDependency.dll":{}},"resources":{"lib/net10.0/de/Example.resources.dll":{"locale":"de"}}}}}}' |
        Set-Content -LiteralPath (Join-Path $testRoot 'CameraTest.deps.json')
    function Assert-Rejected([string]$Expected) {
        $message = ''
        try { Assert-CameraTestPayload $testRoot } catch { $message = $_.Exception.Message }
        if (!$message.Contains($Expected)) { throw "Expected rejection '$Expected', got '$message'" }
    }
    Assert-Rejected 'MissingDependency.dll'
    [IO.File]::WriteAllBytes((Join-Path $testRoot 'MissingDependency.dll'), [byte[]]@(0))
    Assert-Rejected 'Example.resources.dll'
    [IO.Directory]::CreateDirectory((Join-Path $testRoot 'de')) | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $testRoot 'de/Example.resources.dll'), [byte[]]@(0))
    Assert-CameraTestPayload $testRoot
    [IO.File]::WriteAllBytes((Join-Path $testRoot 'coreclr.dll'), [byte[]]@(0))
    Assert-Rejected 'must not embed'
    Write-Host 'CameraTest package contract checks passed: clean contents, camera dependencies, missing assets, cultures, installed .NET.'
} finally {
    $resolved = (Resolve-Path -LiteralPath $testRoot).Path
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
    if ($resolved -ne $testRoot -or (Split-Path $resolved -Parent) -ne $expectedParent -or
        !(Split-Path $resolved -Leaf).StartsWith('CameraTest-package-contract-')) { throw 'Unsafe test cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
