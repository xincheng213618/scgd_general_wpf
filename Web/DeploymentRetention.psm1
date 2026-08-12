Set-StrictMode -Version Latest

function Get-BackupDirectoryBytes {
    param([Parameter(Mandatory)][string]$Path)

    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse -ErrorAction Stop)
    if ($files.Count -eq 0) {
        return 0L
    }
    return [long](($files | Measure-Object Length -Sum).Sum)
}

function Invoke-WebDeployBackupRetention {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$BackupRoot,
        [Parameter(Mandatory)][string]$CurrentBackupPath,
        [ValidateRange(2, 1000)][int]$KeepSuccessful = 10,
        [ValidateRange(1, 1000)][int]$KeepFailed = 3
    )

    $resolvedRoot = [IO.Path]::GetFullPath($BackupRoot).TrimEnd('\')
    $resolvedCurrent = [IO.Path]::GetFullPath($CurrentBackupPath).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
        throw "Deployment backup root does not exist: $resolvedRoot"
    }
    if (-not (Test-Path -LiteralPath $resolvedCurrent -PathType Container)) {
        throw "Current deployment backup does not exist: $resolvedCurrent"
    }
    if (-not [string]::Equals(
        [IO.Path]::GetDirectoryName($resolvedCurrent),
        $resolvedRoot,
        [StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Current deployment backup is not a direct child of the backup root: $resolvedCurrent"
    }
    if ([IO.Path]::GetFileName($resolvedCurrent) -notmatch '^\d{8}-\d{6}$') {
        throw "Current deployment backup has an unexpected directory name: $resolvedCurrent"
    }
    if (
        -not (Test-Path -LiteralPath (Join-Path $resolvedCurrent 'deployment-after.json') -PathType Leaf) -or
        (Test-Path -LiteralPath (Join-Path $resolvedCurrent 'deployment-failed.json') -PathType Leaf)
    ) {
        throw "Current deployment backup is not marked as successful: $resolvedCurrent"
    }

    $allDirectories = @(Get-ChildItem -LiteralPath $resolvedRoot -Directory -ErrorAction Stop)
    $successful = @()
    $failed = @()
    $unclassified = @()
    foreach ($directory in $allDirectories) {
        if ($directory.Name -notmatch '^\d{8}-\d{6}$') {
            $unclassified += $directory
            continue
        }
        if (Test-Path -LiteralPath (Join-Path $directory.FullName 'deployment-failed.json') -PathType Leaf) {
            $failed += $directory
        } elseif (Test-Path -LiteralPath (Join-Path $directory.FullName 'deployment-after.json') -PathType Leaf) {
            $successful += $directory
        } else {
            $unclassified += $directory
        }
    }

    $successful = @($successful | Sort-Object Name -Descending)
    $failed = @($failed | Sort-Object Name -Descending)
    $protectedPaths = @(
        @($successful | Select-Object -First $KeepSuccessful | ForEach-Object { $_.FullName })
        @($failed | Select-Object -First $KeepFailed | ForEach-Object { $_.FullName })
        $resolvedCurrent
    )
    $candidates = @(
        @(
            @($successful | Select-Object -Skip $KeepSuccessful)
            @($failed | Select-Object -Skip $KeepFailed)
        ) | Where-Object {
            $candidatePath = [IO.Path]::GetFullPath($_.FullName).TrimEnd('\')
            $protectedPaths -notcontains $candidatePath
        }
    )

    $removedSuccessful = 0
    $removedFailed = 0
    $removedBytes = 0L
    foreach ($candidate in $candidates) {
        $resolvedCandidate = [IO.Path]::GetFullPath($candidate.FullName).TrimEnd('\')
        if (-not [string]::Equals(
            [IO.Path]::GetDirectoryName($resolvedCandidate),
            $resolvedRoot,
            [StringComparison]::OrdinalIgnoreCase
        )) {
            throw "Refusing to remove a backup outside the backup root: $resolvedCandidate"
        }
        if ($candidate.Name -notmatch '^\d{8}-\d{6}$') {
            throw "Refusing to remove an unexpected backup directory: $resolvedCandidate"
        }
        if ([string]::Equals($resolvedCandidate, $resolvedCurrent, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove the current deployment backup: $resolvedCandidate"
        }
        $reparsePoints = @(
            Get-ChildItem -LiteralPath $resolvedCandidate -Force -Recurse -ErrorAction Stop |
                Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }
        )
        if ($reparsePoints.Count -gt 0) {
            throw "Refusing to remove a deployment backup containing a reparse point: $resolvedCandidate"
        }

        $removedBytes += Get-BackupDirectoryBytes -Path $resolvedCandidate
        $wasFailed = Test-Path -LiteralPath (Join-Path $resolvedCandidate 'deployment-failed.json') -PathType Leaf
        Remove-Item -LiteralPath $resolvedCandidate -Recurse -Force -ErrorAction Stop
        if (Test-Path -LiteralPath $resolvedCandidate) {
            throw "Deployment backup still exists after removal: $resolvedCandidate"
        }
        if ($wasFailed) {
            $removedFailed++
        } else {
            $removedSuccessful++
        }
    }

    return [pscustomobject]@{
        status = 'success'
        keep_successful = $KeepSuccessful
        keep_failed = $KeepFailed
        before_count = $allDirectories.Count
        after_count = $allDirectories.Count - $candidates.Count
        removed_count = $candidates.Count
        removed_successful = $removedSuccessful
        removed_failed = $removedFailed
        removed_bytes = $removedBytes
        preserved_unclassified = $unclassified.Count
    }
}

Export-ModuleMember -Function Invoke-WebDeployBackupRetention
