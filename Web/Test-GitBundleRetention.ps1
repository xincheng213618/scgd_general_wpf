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

function Invoke-TestGit {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string[]]$ArgumentList,
        [switch]$ReturnText
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $script:GitExe -C $Repository @ArgumentList 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($exitCode -ne 0) {
        throw "git $($ArgumentList -join ' ') failed: $($output -join ' ')"
    }
    if ($ReturnText) {
        return (($output -join "`n").Trim())
    }
}

$script:GitExe = (Get-Command git -ErrorAction Stop).Source
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ("cv-web-bundle-retention-" + [Guid]::NewGuid().ToString('N'))))
$systemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
if (-not $temporaryRoot.StartsWith($systemTemporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Generated test path is outside the system temporary directory: $temporaryRoot"
}

try {
    $repository = Join-Path $temporaryRoot 'repo'
    $bundleRoot = Join-Path $temporaryRoot 'bundles'
    New-Item -ItemType Directory -Path $repository | Out-Null
    New-Item -ItemType Directory -Path $bundleRoot | Out-Null
    Invoke-TestGit -Repository $repository -ArgumentList @('init')
    Invoke-TestGit -Repository $repository -ArgumentList @('config', 'user.email', 'bundle-test@colorvision.local')
    Invoke-TestGit -Repository $repository -ArgumentList @('config', 'user.name', 'ColorVision Bundle Test')

    $bundlePaths = @()
    for ($index = 1; $index -le 5; $index++) {
        "commit-$index" | Set-Content -LiteralPath (Join-Path $repository 'payload.txt') -Encoding UTF8
        Invoke-TestGit -Repository $repository -ArgumentList @('add', 'payload.txt')
        Invoke-TestGit -Repository $repository -ArgumentList @('commit', '-m', "commit $index")
        $head = Invoke-TestGit -Repository $repository -ArgumentList @('rev-parse', 'HEAD') -ReturnText
        $abbreviationLength = if ($index -eq 3) { 8 } else { 9 }
        $name = "ColorVision-Web-$($head.Substring(0, $abbreviationLength))-2026010$index-000000.bundle"
        $path = Join-Path $bundleRoot $name
        Invoke-TestGit -Repository $repository -ArgumentList @('bundle', 'create', $path, 'HEAD')
        (Get-Item -LiteralPath $path).LastWriteTimeUtc = [DateTime]::SpecifyKind(
            [DateTime]::Parse("2026-01-0$index 00:00:00"),
            [DateTimeKind]::Utc
        )
        $bundlePaths += $path
    }
    $deployedCommit = Invoke-TestGit -Repository $repository -ArgumentList @('rev-parse', 'HEAD') -ReturnText
    $mainBranch = Invoke-TestGit -Repository $repository -ArgumentList @('branch', '--show-current') -ReturnText

    Invoke-TestGit -Repository $repository -ArgumentList @('checkout', '-b', 'divergent', 'HEAD~3')
    'divergent' | Set-Content -LiteralPath (Join-Path $repository 'divergent.txt') -Encoding UTF8
    Invoke-TestGit -Repository $repository -ArgumentList @('add', 'divergent.txt')
    Invoke-TestGit -Repository $repository -ArgumentList @('commit', '-m', 'divergent commit')
    $divergentHead = Invoke-TestGit -Repository $repository -ArgumentList @('rev-parse', 'HEAD') -ReturnText
    $divergentBundle = Join-Path $bundleRoot "ColorVision-Web-$($divergentHead.Substring(0, 9))-head.bundle"
    Invoke-TestGit -Repository $repository -ArgumentList @('bundle', 'create', $divergentBundle, 'HEAD')
    (Get-Item -LiteralPath $divergentBundle).LastWriteTimeUtc = [DateTime]::SpecifyKind(
        [DateTime]::Parse('2026-01-06 00:00:00'),
        [DateTimeKind]::Utc
    )
    Invoke-TestGit -Repository $repository -ArgumentList @('checkout', $mainBranch)

    $headlessBundle = Join-Path $bundleRoot "ColorVision-Web-$($deployedCommit.Substring(0, 9)).bundle"
    Invoke-TestGit -Repository $repository -ArgumentList @('bundle', 'create', $headlessBundle, $mainBranch)
    (Get-Item -LiteralPath $headlessBundle).LastWriteTimeUtc = [DateTime]::SpecifyKind(
        [DateTime]::Parse('2026-01-07 00:00:00'),
        [DateTimeKind]::Utc
    )

    $corruptBundle = Join-Path $bundleRoot 'ColorVision-Web-deadbeef0-head.bundle'
    'not a git bundle' | Set-Content -LiteralPath $corruptBundle -Encoding UTF8
    $unclassifiedBundle = Join-Path $bundleRoot 'manual-recovery.bundle'
    Copy-Item -LiteralPath $bundlePaths[4] -Destination $unclassifiedBundle

    Import-Module (Join-Path $PSScriptRoot 'GitBundleRetention.psm1') -Force
    $plan = Invoke-WebGitBundleRetention `
        -RepositoryPath $repository `
        -BundleRoot $bundleRoot `
        -DeployedCommit $deployedCommit `
        -KeepCount 2 `
        -CurrentBundlePath $bundlePaths[0] `
        -GitExe $script:GitExe `
        -PlanOnly

    Assert-Equal 'plan' $plan.status 'Unexpected plan status.'
    Assert-Equal 9 $plan.before_count 'Unexpected starting bundle count.'
    Assert-Equal 5 $plan.eligible_count 'Unexpected eligible bundle count.'
    Assert-Equal 2 $plan.planned_remove_count 'Unexpected planned removal count.'
    Assert-Equal 0 $plan.removed_count 'Plan-only retention removed a bundle.'
    Assert-Equal $true $plan.preserved_current 'Current bundle was not identified as protected.'
    Assert-Equal 1 $plan.preserved_unclassified 'Unexpected unclassified count.'
    Assert-Equal 1 $plan.preserved_unverified 'Unexpected unverified count.'
    Assert-Equal 1 $plan.preserved_without_head 'Unexpected HEAD-less count.'
    Assert-Equal 1 $plan.preserved_unreachable 'Unexpected unreachable count.'
    Assert-Equal $true (Test-Path -LiteralPath $bundlePaths[1]) 'Plan-only retention deleted a candidate.'

    $result = Invoke-WebGitBundleRetention `
        -RepositoryPath $repository `
        -BundleRoot $bundleRoot `
        -DeployedCommit $deployedCommit `
        -KeepCount 2 `
        -CurrentBundlePath $bundlePaths[0] `
        -GitExe $script:GitExe

    Assert-Equal 'success' $result.status 'Unexpected retention status.'
    Assert-Equal 2 $result.removed_count 'Unexpected removal count.'
    Assert-Equal 7 $result.after_count 'Unexpected final bundle count.'
    Assert-Equal $true ($result.removed_bytes -gt 0) 'Removed byte count was not recorded.'
    Assert-Equal $true (Test-Path -LiteralPath $bundlePaths[0]) 'Current bundle was removed.'
    Assert-Equal $false (Test-Path -LiteralPath $bundlePaths[1]) 'Old eligible bundle remains.'
    Assert-Equal $false (Test-Path -LiteralPath $bundlePaths[2]) 'Eight-character eligible bundle remains.'
    Assert-Equal $true (Test-Path -LiteralPath $bundlePaths[3]) 'Newest retained bundle was removed.'
    Assert-Equal $true (Test-Path -LiteralPath $bundlePaths[4]) 'Newest retained bundle was removed.'
    Assert-Equal $true (Test-Path -LiteralPath $divergentBundle) 'Unreachable bundle was removed.'
    Assert-Equal $true (Test-Path -LiteralPath $headlessBundle) 'HEAD-less bundle was removed.'
    Assert-Equal $true (Test-Path -LiteralPath $corruptBundle) 'Unverified bundle was removed.'
    Assert-Equal $true (Test-Path -LiteralPath $unclassifiedBundle) 'Unclassified bundle was removed.'

    $invalidCommitThrew = $false
    try {
        Invoke-WebGitBundleRetention `
            -RepositoryPath $repository `
            -BundleRoot $bundleRoot `
            -DeployedCommit ('0' * 40) `
            -KeepCount 1 `
            -GitExe $script:GitExe | Out-Null
    } catch {
        $invalidCommitThrew = $true
    }
    Assert-Equal $true $invalidCommitThrew 'An unavailable deployed commit was accepted.'

    Write-Output ($result | ConvertTo-Json -Compress)
} finally {
    $resolvedCleanup = [IO.Path]::GetFullPath($temporaryRoot)
    if (
        (Test-Path -LiteralPath $resolvedCleanup) -and
        $resolvedCleanup.StartsWith($systemTemporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedCleanup).StartsWith('cv-web-bundle-retention-', [StringComparison]::OrdinalIgnoreCase)
    ) {
        Remove-Item -LiteralPath $resolvedCleanup -Recurse -Force
    }
}
