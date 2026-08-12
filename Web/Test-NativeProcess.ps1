[CmdletBinding()]
param(
    [switch]$ArgumentProbe,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$ProbeArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($ArgumentProbe) {
    [Console]::Out.Write(($ProbeArguments | ConvertTo-Json -Compress))
    exit 0
}

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

Import-Module (Join-Path $PSScriptRoot 'NativeProcess.psm1') -Force

$missingExecutableFailed = $false
$missingExecutable = Join-Path $env:TEMP ('missing-colorvision-native-process-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    Invoke-WebNativeProcess `
        -FilePath $missingExecutable `
        -ArgumentList @() `
        -TimeoutSeconds 1 | Out-Null
} catch {
    $missingExecutableFailed = $true
    if ($_.Exception.Message -like '*No process is associated*') {
        throw 'Startup failure was hidden by process cleanup.'
    }
}
Assert-Equal $true $missingExecutableFailed 'A missing executable did not fail.'

$expectedArguments = @('plain', 'with space', 'quote"value', 'C:\trailing\')
$argumentList = @('-NoProfile', '-NonInteractive', '-File', $PSCommandPath, '-ArgumentProbe') + $expectedArguments
$argumentResult = Invoke-WebNativeProcess `
    -FilePath 'powershell.exe' `
    -ArgumentList $argumentList `
    -TimeoutSeconds 5
Assert-Equal 0 $argumentResult.ExitCode 'The argument probe failed.'
$actualArguments = [string[]]($argumentResult.StdOut | ConvertFrom-Json)
Assert-Equal ($expectedArguments -join '|') ($actualArguments -join '|') 'Native arguments changed during quoting.'

$successScript = @'
[Console]::Out.Write('native-ok')
[Console]::Error.Write('native-err')
exit 7
'@
$successPayload = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($successScript))
$success = Invoke-WebNativeProcess `
    -FilePath 'powershell.exe' `
    -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', $successPayload) `
    -TimeoutSeconds 5
Assert-Equal $false $success.TimedOut 'A short child process timed out.'
Assert-Equal 7 $success.ExitCode 'The child exit code changed.'
Assert-Equal 'native-ok' $success.StdOut 'Standard output was not captured.'
Assert-Equal 'native-err' $success.StdErr 'Standard error was not captured.'

$childScript = '[Threading.Thread]::Sleep(30000)'
$childPayload = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($childScript))
$parentScript = @"
`$child = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', '$childPayload') -WindowStyle Hidden -PassThru
[Console]::Out.WriteLine('CHILD_PID=' + `$child.Id)
[Console]::Out.Flush()
Wait-Process -Id `$child.Id
"@
$parentPayload = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($parentScript))
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$timeout = Invoke-WebNativeProcess `
    -FilePath 'powershell.exe' `
    -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', $parentPayload) `
    -TimeoutSeconds 2
$stopwatch.Stop()
$childMatch = [regex]::Match($timeout.StdOut, 'CHILD_PID=(\d+)')
if (-not $childMatch.Success) {
    throw "Timed process did not report its child PID: $($timeout.StdOut)"
}
$childPid = [int]$childMatch.Groups[1].Value
Assert-Equal $true $timeout.TimedOut 'The long-running process did not time out.'
Assert-Equal $false ([bool](Get-Process -Id $timeout.ProcessId -ErrorAction SilentlyContinue)) 'The timed-out parent is still running.'
Assert-Equal $false ([bool](Get-Process -Id $childPid -ErrorAction SilentlyContinue)) 'The timed-out child is still running.'
if ($stopwatch.Elapsed.TotalSeconds -gt 8) {
    throw "The hard timeout took too long: $($stopwatch.Elapsed.TotalSeconds) seconds."
}

$deploySource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Deploy-Nas.ps1') -Raw
Assert-Equal $true $deploySource.Contains(
    'Invoke-GitNetworkCommand -ArgumentList @(''-C'', $repoPath, ''fetch'', ''origin'')'
) 'Origin fetch is not using the bounded network process.'
Assert-Equal $true $deploySource.Contains(
    'Invoke-NativeCommand -FilePath $gitExe -ArgumentList @(''-C'', $repoPath, ''merge'', ''--ff-only'', "origin/$branch")'
) 'Origin deployment is not merging the already-fetched ref.'
Assert-Equal $false $deploySource.Contains("'pull', '--ff-only'") 'Origin deployment still opens a second network connection with pull.'
Assert-Equal 1 ([regex]::Matches($deploySource, [regex]::Escape("'fetch', 'origin'")).Count) 'Origin fetch should occur exactly once.'

[ordered]@{
    status = 'success'
    startup_failure_preserved = $true
    argument_count = $actualArguments.Count
    captured_exit_code = $success.ExitCode
    timeout_seconds = 2
    elapsed_ms = [int]$stopwatch.Elapsed.TotalMilliseconds
    process_tree_stopped = $true
    origin_fetch_count = 1
    duplicate_pull_removed = $true
} | ConvertTo-Json -Compress
