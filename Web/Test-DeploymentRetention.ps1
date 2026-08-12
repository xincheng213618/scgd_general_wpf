[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal {
    param(
        [Parameter(Mandatory)]$Expected,
        [Parameter(Mandatory)]$Actual,
        [Parameter(Mandatory)][string]$Message
    )
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ("cv-web-retention-" + [Guid]::NewGuid().ToString('N'))))
$systemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
if (-not $temporaryRoot.StartsWith($systemTemporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Generated test path is outside the system temporary directory: $temporaryRoot"
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Import-Module (Join-Path $PSScriptRoot 'DeploymentRetention.psm1') -Force

    foreach ($name in @(
        '20260101-000001',
        '20260101-000002',
        '20260101-000003',
        '20260101-000004',
        '20260101-000005'
    )) {
        $path = Join-Path $temporaryRoot $name
        New-Item -ItemType Directory -Path $path | Out-Null
        '{}' | Set-Content -LiteralPath (Join-Path $path 'deployment-after.json') -Encoding UTF8
        [IO.File]::WriteAllBytes((Join-Path $path 'payload.bin'), (New-Object byte[] 1024))
    }
    foreach ($name in @('20260102-000001', '20260102-000002', '20260102-000003')) {
        $path = Join-Path $temporaryRoot $name
        New-Item -ItemType Directory -Path $path | Out-Null
        '{}' | Set-Content -LiteralPath (Join-Path $path 'deployment-failed.json') -Encoding UTF8
        [IO.File]::WriteAllBytes((Join-Path $path 'payload.bin'), (New-Object byte[] 512))
    }

    $unknownTimestamp = Join-Path $temporaryRoot '20260103-000001'
    New-Item -ItemType Directory -Path $unknownTimestamp | Out-Null
    $unexpectedName = Join-Path $temporaryRoot 'keep-me'
    New-Item -ItemType Directory -Path $unexpectedName | Out-Null
    '{}' | Set-Content -LiteralPath (Join-Path $unexpectedName 'deployment-after.json') -Encoding UTF8

    $currentBackup = Join-Path $temporaryRoot '20260101-000005'
    $result = Invoke-WebDeployBackupRetention `
        -BackupRoot $temporaryRoot `
        -CurrentBackupPath $currentBackup `
        -KeepSuccessful 2 `
        -KeepFailed 1

    Assert-Equal 10 $result.before_count 'Unexpected starting directory count.'
    Assert-Equal 5 $result.after_count 'Unexpected retained directory count.'
    Assert-Equal 5 $result.removed_count 'Unexpected removed directory count.'
    Assert-Equal 3 $result.removed_successful 'Unexpected removed success count.'
    Assert-Equal 2 $result.removed_failed 'Unexpected removed failure count.'
    Assert-Equal 2 $result.preserved_unclassified 'Unexpected unclassified count.'
    Assert-Equal $true (Test-Path -LiteralPath $currentBackup) 'Current backup was removed.'
    Assert-Equal $true (Test-Path -LiteralPath $unknownTimestamp) 'Unclassified timestamp directory was removed.'
    Assert-Equal $true (Test-Path -LiteralPath $unexpectedName) 'Unexpected directory was removed.'
    Assert-Equal $false (Test-Path -LiteralPath (Join-Path $temporaryRoot '20260101-000001')) 'Old successful backup remains.'
    Assert-Equal $false (Test-Path -LiteralPath (Join-Path $temporaryRoot '20260102-000001')) 'Old failed backup remains.'

    $clockSkewRoot = Join-Path $temporaryRoot 'clock-skew'
    New-Item -ItemType Directory -Path $clockSkewRoot | Out-Null
    foreach ($name in @('20260104-000001', '20260104-000002', '20260104-000003', '20260104-000004')) {
        $path = Join-Path $clockSkewRoot $name
        New-Item -ItemType Directory -Path $path | Out-Null
        '{}' | Set-Content -LiteralPath (Join-Path $path 'deployment-after.json') -Encoding UTF8
    }
    $clockSkewCurrent = Join-Path $clockSkewRoot '20260104-000001'
    $clockSkewResult = Invoke-WebDeployBackupRetention `
        -BackupRoot $clockSkewRoot `
        -CurrentBackupPath $clockSkewCurrent `
        -KeepSuccessful 2 `
        -KeepFailed 1
    Assert-Equal 1 $clockSkewResult.removed_count 'Unexpected clock-skew removal count.'
    Assert-Equal $true (Test-Path -LiteralPath $clockSkewCurrent) 'Clock-skewed current backup was removed.'
    Assert-Equal $false (Test-Path -LiteralPath (Join-Path $clockSkewRoot '20260104-000002')) 'Old unprotected backup remains.'

    $productionShapeRoot = Join-Path $temporaryRoot 'production-shape'
    New-Item -ItemType Directory -Path $productionShapeRoot | Out-Null
    for ($index = 1; $index -le 38; $index++) {
        $name = '20260105-' + $index.ToString('000000')
        $path = Join-Path $productionShapeRoot $name
        New-Item -ItemType Directory -Path $path | Out-Null
        '{}' | Set-Content -LiteralPath (Join-Path $path 'deployment-after.json') -Encoding UTF8
    }
    foreach ($name in @('20260106-000001', '20260106-000002')) {
        $path = Join-Path $productionShapeRoot $name
        New-Item -ItemType Directory -Path $path | Out-Null
        '{}' | Set-Content -LiteralPath (Join-Path $path 'deployment-failed.json') -Encoding UTF8
    }
    $productionShapeResult = Invoke-WebDeployBackupRetention `
        -BackupRoot $productionShapeRoot `
        -CurrentBackupPath (Join-Path $productionShapeRoot '20260105-000038')
    Assert-Equal 40 $productionShapeResult.before_count 'Unexpected production-shape starting count.'
    Assert-Equal 12 $productionShapeResult.after_count 'Unexpected production-shape retained count.'
    Assert-Equal 28 $productionShapeResult.removed_count 'Unexpected production-shape removal count.'
    Assert-Equal 28 $productionShapeResult.removed_successful 'Unexpected production-shape success removal count.'
    Assert-Equal 0 $productionShapeResult.removed_failed 'Production-shape failed evidence was removed.'

    $outsideRoot = Join-Path ([IO.Path]::GetTempPath()) ("cv-web-retention-outside-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $outsideRoot | Out-Null
    try {
        $threw = $false
        try {
            Invoke-WebDeployBackupRetention -BackupRoot $temporaryRoot -CurrentBackupPath $outsideRoot | Out-Null
        } catch {
            $threw = $true
        }
        Assert-Equal $true $threw 'An outside current backup path was accepted.'
    } finally {
        $resolvedOutside = [IO.Path]::GetFullPath($outsideRoot)
        if ($resolvedOutside.StartsWith($systemTemporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedOutside -Recurse -Force
        }
    }

    Write-Output ($result | ConvertTo-Json -Compress)
} finally {
    $resolvedCleanup = [IO.Path]::GetFullPath($temporaryRoot)
    if (
        (Test-Path -LiteralPath $resolvedCleanup) -and
        $resolvedCleanup.StartsWith($systemTemporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedCleanup).StartsWith('cv-web-retention-', [StringComparison]::OrdinalIgnoreCase)
    ) {
        Remove-Item -LiteralPath $resolvedCleanup -Recurse -Force
    }
}
