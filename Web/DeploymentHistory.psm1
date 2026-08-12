Set-StrictMode -Version Latest

function Write-WebDeploymentHistory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$HistoryPath,
        [Parameter(Mandatory)]$Record,
        [ValidateRange(20, 100000)][int]$KeepRecords = 500
    )

    $resolvedPath = [IO.Path]::GetFullPath($HistoryPath)
    $parentPath = [IO.Path]::GetDirectoryName($resolvedPath)
    if (-not $parentPath -or -not (Test-Path -LiteralPath $parentPath -PathType Container)) {
        throw "Deployment history directory does not exist: $parentPath"
    }
    if (Test-Path -LiteralPath $resolvedPath) {
        $historyItem = Get-Item -LiteralPath $resolvedPath -Force
        if (-not $historyItem.PSIsContainer -and ($historyItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Deployment history file must not be a reparse point: $resolvedPath"
        }
        if ($historyItem.PSIsContainer) {
            throw "Deployment history path is not a file: $resolvedPath"
        }
    }

    $existingLines = @()
    if (Test-Path -LiteralPath $resolvedPath -PathType Leaf) {
        $existingLines = @([IO.File]::ReadAllLines($resolvedPath))
    }
    foreach ($line in $existingLines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            throw 'Deployment history contains a blank record; refusing to rewrite it.'
        }
        try {
            $parsed = $line | ConvertFrom-Json -ErrorAction Stop
        } catch {
            throw "Deployment history contains malformed JSON; refusing to rewrite it: $($_.Exception.Message)"
        }
        if ($null -eq $parsed -or $parsed -isnot [PSCustomObject]) {
            throw 'Deployment history contains a non-object record; refusing to rewrite it.'
        }
    }

    $beforeCount = $existingLines.Count
    $removedCount = [Math]::Max(0, $beforeCount + 1 - $KeepRecords)
    $removedBytes = 0L
    $utf8 = New-Object Text.UTF8Encoding($false)
    if ($removedCount -gt 0) {
        foreach ($line in @($existingLines | Select-Object -First $removedCount)) {
            $removedBytes += $utf8.GetByteCount($line + [Environment]::NewLine)
        }
    }

    $retention = [ordered]@{
        status = 'success'
        keep_records = $KeepRecords
        before_count = $beforeCount
        after_count = [Math]::Min($beforeCount + 1, $KeepRecords)
        removed_count = $removedCount
        removed_bytes = $removedBytes
    }
    if ($Record -is [Collections.IDictionary]) {
        $Record['history_retention'] = $retention
    } else {
        $Record | Add-Member -NotePropertyName history_retention -NotePropertyValue $retention -Force
    }

    $newLine = $Record | ConvertTo-Json -Compress -Depth 8
    $retainedLines = @(
        @($existingLines | Select-Object -Skip $removedCount)
        $newLine
    )
    $temporaryPath = Join-Path $parentPath ('.' + [IO.Path]::GetFileName($resolvedPath) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $replacementBackupPath = $temporaryPath + '.previous'
    try {
        [IO.File]::WriteAllLines($temporaryPath, $retainedLines, $utf8)
        if (Test-Path -LiteralPath $resolvedPath -PathType Leaf) {
            [IO.File]::Replace($temporaryPath, $resolvedPath, $replacementBackupPath, $true)
        } else {
            [IO.File]::Move($temporaryPath, $resolvedPath)
        }
    } finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
        if (Test-Path -LiteralPath $replacementBackupPath -PathType Leaf) {
            Remove-Item -LiteralPath $replacementBackupPath -Force
        }
    }

    return [pscustomobject]$retention
}

Export-ModuleMember -Function Write-WebDeploymentHistory
