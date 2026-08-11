[CmdletBinding(SupportsShouldProcess = $true)]
param()

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$repositoryPrefix = $repositoryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.git'))) {
    throw "Refusing to clean because the resolved root is not a Git checkout: $repositoryRoot"
}

$excludedDirectoryNames = @('.git', '.venv', '.vs', 'node_modules', 'packages')
$buildDirectories = Get-ChildItem -LiteralPath $repositoryRoot -Directory -Recurse -Force |
    Where-Object {
        if ($_.Name -notin @('bin', 'obj')) {
            return $false
        }

        $relativePath = $_.FullName.Substring($repositoryPrefix.Length)
        $segments = $relativePath -split '[\\/]'
        -not ($segments | Where-Object { $_ -in $excludedDirectoryNames })
    } |
    Sort-Object { $_.FullName.Length } -Descending

$removedCount = 0
foreach ($buildDirectory in $buildDirectories) {
    if (-not (Test-Path -LiteralPath $buildDirectory.FullName)) {
        continue
    }

    $resolvedTarget = (Resolve-Path -LiteralPath $buildDirectory.FullName).Path
    if (-not $resolvedTarget.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the repository: $resolvedTarget"
    }

    if ($PSCmdlet.ShouldProcess($resolvedTarget, 'Remove build output directory')) {
        Remove-Item -LiteralPath $resolvedTarget -Force -Recurse
        $removedCount++
    }
}

Write-Host "Removed $removedCount bin/obj director$(if ($removedCount -eq 1) { 'y' } else { 'ies' }) under $repositoryRoot."
