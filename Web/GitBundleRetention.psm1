Set-StrictMode -Version Latest

$script:BundleNamePattern = '^ColorVision-Web-[0-9a-f]{8,40}(?:-head)?(?:-\d{8}-\d{6})?\.bundle$'

function Invoke-GitCapture {
    param(
        [Parameter(Mandatory)][string]$GitExe,
        [Parameter(Mandatory)][string]$RepositoryPath,
        [Parameter(Mandatory)][string[]]$ArgumentList
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $GitExe -C $RepositoryPath @ArgumentList 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    return [pscustomobject]@{
        exit_code = $exitCode
        output = (($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
    }
}

function Get-GitBundleClassification {
    param(
        [Parameter(Mandatory)][System.IO.FileInfo]$File,
        [Parameter(Mandatory)][string]$RepositoryPath,
        [Parameter(Mandatory)][string]$DeployedCommit,
        [Parameter(Mandatory)][string]$GitExe
    )

    if ($File.Name -notmatch $script:BundleNamePattern) {
        return [pscustomobject]@{ classification = 'unclassified'; head = $null }
    }
    if (($File.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        return [pscustomobject]@{ classification = 'reparse_point'; head = $null }
    }

    $verify = Invoke-GitCapture -GitExe $GitExe -RepositoryPath $RepositoryPath `
        -ArgumentList @('bundle', 'verify', $File.FullName)
    if ($verify.exit_code -ne 0) {
        return [pscustomobject]@{ classification = 'unverified'; head = $null }
    }

    $heads = Invoke-GitCapture -GitExe $GitExe -RepositoryPath $RepositoryPath `
        -ArgumentList @('bundle', 'list-heads', $File.FullName)
    if ($heads.exit_code -ne 0) {
        return [pscustomobject]@{ classification = 'unverified'; head = $null }
    }
    $headLines = @($heads.output -split "`n" | Where-Object { $_ -match '\sHEAD$' })
    if ($headLines.Count -ne 1) {
        return [pscustomobject]@{ classification = 'without_head'; head = $null }
    }
    $head = ($headLines[0] -split '\s+')[0]

    $reachable = Invoke-GitCapture -GitExe $GitExe -RepositoryPath $RepositoryPath `
        -ArgumentList @('merge-base', '--is-ancestor', $head, $DeployedCommit)
    if ($reachable.exit_code -eq 1) {
        return [pscustomobject]@{ classification = 'unreachable'; head = $head }
    }
    if ($reachable.exit_code -ne 0) {
        return [pscustomobject]@{ classification = 'unverified'; head = $head }
    }
    return [pscustomobject]@{ classification = 'eligible'; head = $head }
}

function New-EmptyRetentionResult {
    param([int]$KeepCount, [bool]$PlanOnly)

    return [pscustomobject]@{
        status = if ($PlanOnly) { 'plan' } else { 'success' }
        keep_count = $KeepCount
        before_count = 0
        after_count = 0
        eligible_count = 0
        planned_remove_count = 0
        planned_remove_bytes = 0L
        removed_count = 0
        removed_bytes = 0L
        preserved_current = $false
        preserved_unclassified = 0
        preserved_unverified = 0
        preserved_without_head = 0
        preserved_unreachable = 0
        preserved_reparse_points = 0
        errors = @()
        candidate_names = @()
    }
}

function Invoke-WebGitBundleRetention {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryPath,
        [Parameter(Mandatory)][string]$BundleRoot,
        [Parameter(Mandatory)][string]$DeployedCommit,
        [ValidateRange(1, 1000)][int]$KeepCount = 3,
        [string]$CurrentBundlePath = '',
        [string]$GitExe = 'git',
        [switch]$PlanOnly
    )

    $resolvedRoot = [IO.Path]::GetFullPath($BundleRoot).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
        return New-EmptyRetentionResult -KeepCount $KeepCount -PlanOnly ([bool]$PlanOnly)
    }
    $resolvedRepository = [IO.Path]::GetFullPath($RepositoryPath).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $resolvedRepository -PathType Container)) {
        throw "Git repository does not exist: $resolvedRepository"
    }
    $commitCheck = Invoke-GitCapture -GitExe $GitExe -RepositoryPath $resolvedRepository `
        -ArgumentList @('cat-file', '-e', "$DeployedCommit^{commit}")
    if ($commitCheck.exit_code -ne 0) {
        throw "Deployed commit is unavailable in the NAS repository: $DeployedCommit"
    }

    $resolvedCurrent = if ([string]::IsNullOrWhiteSpace($CurrentBundlePath)) {
        ''
    } else {
        [IO.Path]::GetFullPath($CurrentBundlePath)
    }
    $files = @(Get-ChildItem -LiteralPath $resolvedRoot -File -Filter '*.bundle' -ErrorAction Stop)
    $eligible = @()
    $counts = @{
        unclassified = 0
        unverified = 0
        without_head = 0
        unreachable = 0
        reparse_point = 0
    }
    $preservedCurrent = $false

    foreach ($file in $files) {
        $classification = Get-GitBundleClassification `
            -File $file `
            -RepositoryPath $resolvedRepository `
            -DeployedCommit $DeployedCommit `
            -GitExe $GitExe
        $resolvedFile = [IO.Path]::GetFullPath($file.FullName)
        if ($resolvedCurrent -and [string]::Equals(
            $resolvedFile,
            $resolvedCurrent,
            [StringComparison]::OrdinalIgnoreCase
        )) {
            $preservedCurrent = $true
        }
        if ($classification.classification -eq 'eligible') {
            $eligible += [pscustomobject]@{
                file = $file
                head = $classification.head
                length = [long]$file.Length
                modified_ticks = $file.LastWriteTimeUtc.Ticks
                is_current = ($resolvedCurrent -and [string]::Equals(
                    $resolvedFile,
                    $resolvedCurrent,
                    [StringComparison]::OrdinalIgnoreCase
                ))
            }
        } else {
            $counts[$classification.classification]++
        }
    }

    $eligible = @($eligible | Sort-Object `
        @{ Expression = { $_.file.LastWriteTimeUtc }; Descending = $true }, `
        @{ Expression = { $_.file.Name }; Descending = $true })
    $protected = @($eligible | Select-Object -First $KeepCount)
    $candidateItems = @($eligible | Where-Object {
        $item = $_
        -not $item.is_current -and -not @($protected | Where-Object {
            [string]::Equals(
                $_.file.FullName,
                $item.file.FullName,
                [StringComparison]::OrdinalIgnoreCase
            )
        })
    })
    $plannedBytes = if ($candidateItems.Count -eq 0) {
        0L
    } else {
        [long](($candidateItems | Measure-Object -Property length -Sum).Sum)
    }
    $candidateNames = @($candidateItems | ForEach-Object { $_.file.Name })

    if ($PlanOnly) {
        return [pscustomobject]@{
            status = 'plan'
            keep_count = $KeepCount
            before_count = $files.Count
            after_count = $files.Count
            eligible_count = $eligible.Count
            planned_remove_count = $candidateItems.Count
            planned_remove_bytes = $plannedBytes
            removed_count = 0
            removed_bytes = 0L
            preserved_current = $preservedCurrent
            preserved_unclassified = $counts.unclassified
            preserved_unverified = $counts.unverified
            preserved_without_head = $counts.without_head
            preserved_unreachable = $counts.unreachable
            preserved_reparse_points = $counts.reparse_point
            errors = @()
            candidate_names = $candidateNames
        }
    }

    $removedCount = 0
    $removedBytes = 0L
    $errors = @()
    foreach ($candidate in $candidateItems) {
        try {
            $path = [IO.Path]::GetFullPath($candidate.file.FullName)
            if (-not [string]::Equals(
                [IO.Path]::GetDirectoryName($path),
                $resolvedRoot,
                [StringComparison]::OrdinalIgnoreCase
            )) {
                throw "Bundle is outside the configured root: $path"
            }
            if ([IO.Path]::GetFileName($path) -notmatch $script:BundleNamePattern) {
                throw "Bundle name changed during retention: $path"
            }
            if ($resolvedCurrent -and [string]::Equals(
                $path,
                $resolvedCurrent,
                [StringComparison]::OrdinalIgnoreCase
            )) {
                throw "Refusing to remove the current deployment bundle: $path"
            }
            $freshFile = Get-Item -LiteralPath $path -Force -ErrorAction Stop
            if (
                ($freshFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                $freshFile.Length -ne $candidate.length -or
                $freshFile.LastWriteTimeUtc.Ticks -ne $candidate.modified_ticks
            ) {
                throw "Bundle changed during retention: $path"
            }
            $freshClassification = Get-GitBundleClassification `
                -File $freshFile `
                -RepositoryPath $resolvedRepository `
                -DeployedCommit $DeployedCommit `
                -GitExe $GitExe
            if (
                $freshClassification.classification -ne 'eligible' -or
                $freshClassification.head -ne $candidate.head
            ) {
                throw "Bundle is no longer a verified deployed ancestor: $path"
            }

            Remove-Item -LiteralPath $path -Force -ErrorAction Stop
            if (Test-Path -LiteralPath $path) {
                throw "Bundle still exists after removal: $path"
            }
            $removedCount++
            $removedBytes += $candidate.length
        } catch {
            $errors += "$($candidate.file.Name): $($_.Exception.Message)"
        }
    }

    return [pscustomobject]@{
        status = if ($errors.Count -eq 0) { 'success' } else { 'error' }
        keep_count = $KeepCount
        before_count = $files.Count
        after_count = $files.Count - $removedCount
        eligible_count = $eligible.Count
        planned_remove_count = $candidateItems.Count
        planned_remove_bytes = $plannedBytes
        removed_count = $removedCount
        removed_bytes = $removedBytes
        preserved_current = $preservedCurrent
        preserved_unclassified = $counts.unclassified
        preserved_unverified = $counts.unverified
        preserved_without_head = $counts.without_head
        preserved_unreachable = $counts.unreachable
        preserved_reparse_points = $counts.reparse_point
        errors = $errors
        candidate_names = $candidateNames
    }
}

Export-ModuleMember -Function Invoke-WebGitBundleRetention
