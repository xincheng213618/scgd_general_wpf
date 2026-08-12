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

$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('cv-web-history-' + [Guid]::NewGuid().ToString('N'))))
$systemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
if (-not $temporaryRoot.StartsWith($systemTemporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Generated test path is outside the system temporary directory: $temporaryRoot"
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Import-Module (Join-Path $PSScriptRoot 'DeploymentHistory.psm1') -Force
    $historyPath = Join-Path $temporaryRoot 'web-deploy-history.jsonl'

    for ($index = 1; $index -le 24; $index++) {
        $record = [ordered]@{
            timestamp = "2026-08-12T00:00:$($index.ToString('00'))+08:00"
            status = 'success'
            deployed_commit = $index.ToString('x40')
        }
        $result = Write-WebDeploymentHistory -HistoryPath $historyPath -Record $record -KeepRecords 20
    }

    $lines = @([IO.File]::ReadAllLines($historyPath))
    Assert-Equal 20 $lines.Count 'Unexpected retained history count.'
    Assert-Equal 20 $result.before_count 'Unexpected final starting count.'
    Assert-Equal 20 $result.after_count 'Unexpected final retained count.'
    Assert-Equal 1 $result.removed_count 'Unexpected final removal count.'
    $first = $lines[0] | ConvertFrom-Json
    $last = $lines[-1] | ConvertFrom-Json
    Assert-Equal ('5'.PadLeft(40, '0')) $first.deployed_commit 'Old history was not removed in append order.'
    Assert-Equal ('18'.PadLeft(40, '0')) $last.deployed_commit 'Newest history record is missing.'
    Assert-Equal 20 $last.history_retention.keep_records 'Retention evidence was not embedded in the new record.'

    $malformedPath = Join-Path $temporaryRoot 'malformed.jsonl'
    [IO.File]::WriteAllLines($malformedPath, @('{"status":"success"}', '{broken'), (New-Object Text.UTF8Encoding($false)))
    $beforeMalformed = [IO.File]::ReadAllBytes($malformedPath)
    $threw = $false
    try {
        Write-WebDeploymentHistory -HistoryPath $malformedPath -Record ([ordered]@{ status = 'failed' }) -KeepRecords 20 | Out-Null
    } catch {
        $threw = $true
    }
    Assert-Equal $true $threw 'Malformed history was silently rewritten.'
    $afterMalformed = [IO.File]::ReadAllBytes($malformedPath)
    Assert-Equal ([Convert]::ToBase64String($beforeMalformed)) ([Convert]::ToBase64String($afterMalformed)) 'Malformed history changed after rejection.'

    $dryRunRepository = Join-Path $temporaryRoot 'dryrun-repo'
    $dryRunStorage = Join-Path $temporaryRoot 'dryrun-storage'
    New-Item -ItemType Directory -Path (Join-Path $dryRunRepository 'Web') -Force | Out-Null
    New-Item -ItemType Directory -Path $dryRunStorage -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DeploymentHistory.psm1') -Destination (Join-Path $dryRunRepository 'Web\DeploymentHistory.psm1')
    $deployScriptText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Deploy-Nas.ps1') -Raw
    $templateMatch = [regex]::Match($deployScriptText, "(?s)\`$remoteTemplate = @'\r?\n(.*?)\r?\n'@")
    Assert-Equal $true $templateMatch.Success 'Deploy-Nas remote template was not found.'
    $dryRunScript = $templateMatch.Groups[1].Value
    $literalRepository = "'" + $dryRunRepository.Replace("'", "''") + "'"
    $literalStorage = "'" + $dryRunStorage.Replace("'", "''") + "'"
    $dryRunReplacements = [ordered]@{
        '__REPO_PATH__' = $literalRepository
        '__STORAGE_PATH__' = $literalStorage
        '__BRANCH__' = "'develop'"
        '__TASK_PATH__' = "'\ColorVision\'"
        '__TASK_NAME__' = "'ColorVisionWeb'"
        '__PORT__' = '9998'
        '__KEEP_SUCCESSFUL_BACKUPS__' = '10'
        '__KEEP_FAILED_BACKUPS__' = '3'
        '__KEEP_GIT_BUNDLES__' = '3'
        '__KEEP_HISTORY_RECORDS__' = '500'
        '__REMOTE_GIT_BUNDLE__' = "''"
        '__FORCE__' = '$false'
        '__SKIP_TESTS__' = '$false'
        '__DRY_RUN__' = '$true'
    }
    foreach ($replacement in $dryRunReplacements.GetEnumerator()) {
        $dryRunScript = $dryRunScript.Replace($replacement.Key, [string]$replacement.Value)
    }
    $dryRunScriptPath = Join-Path $temporaryRoot 'dryrun-contract.ps1'
    [IO.File]::WriteAllText($dryRunScriptPath, $dryRunScript, (New-Object Text.UTF8Encoding($false)))
    $dryRunOutput = @(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $dryRunScriptPath 2>&1)
    Assert-Equal 1 $LASTEXITCODE 'A failed dry run returned the wrong exit code.'
    Assert-Equal $false (Test-Path -LiteralPath (Join-Path $dryRunStorage 'web-deploy-history.jsonl')) 'A failed dry run mutated deployment history.'
    Assert-Equal $true (($dryRunOutput -join "`n").Contains('DRY_RUN_ERROR=')) 'A failed dry run did not report its distinct error contract.'

    Write-Output ($result | ConvertTo-Json -Compress)
} finally {
    $resolvedCleanup = [IO.Path]::GetFullPath($temporaryRoot)
    if (
        (Test-Path -LiteralPath $resolvedCleanup) -and
        $resolvedCleanup.StartsWith($systemTemporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedCleanup).StartsWith('cv-web-history-', [StringComparison]::OrdinalIgnoreCase)
    ) {
        Remove-Item -LiteralPath $resolvedCleanup -Recurse -Force
    }
}
