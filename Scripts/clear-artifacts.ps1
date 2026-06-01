[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Root
)

if ([string]::IsNullOrWhiteSpace($Root)) {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $Root = $PSScriptRoot
    }
    else {
        $Root = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
}

$Root = (Resolve-Path -LiteralPath $Root).Path

$artifactDirs = Get-ChildItem -Path $Root -Directory -Recurse -Force | Where-Object { $_.Name -eq 'artifacts' }

if (-not $artifactDirs) {
    Write-Host "No artifacts directories found under $Root."
    return
}

$removedCount = 0
$skipped = @()

foreach ($artifactDir in $artifactDirs) {
    $children = @(Get-ChildItem -Path $artifactDir.FullName -Force)

    if ($children.Count -eq 1 -and $children[0].PSIsContainer -and $children[0].Name -eq 'copilot-build') {
        if ($PSCmdlet.ShouldProcess($artifactDir.FullName, 'Remove artifacts directory')) {
            Remove-Item -LiteralPath $artifactDir.FullName -Recurse -Force
            $removedCount++
            Write-Host "Removed $($artifactDir.FullName)"
        }

        continue
    }

    $skipped += $artifactDir.FullName
}

Write-Host "Removed $removedCount artifacts director$(if ($removedCount -eq 1) { 'y' } else { 'ies' })."

if ($skipped.Count -gt 0) {
    Write-Warning "Skipped artifacts directories with unexpected contents:"
    $skipped | ForEach-Object { Write-Warning "  $_" }
}