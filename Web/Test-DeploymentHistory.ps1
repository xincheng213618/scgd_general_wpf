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
