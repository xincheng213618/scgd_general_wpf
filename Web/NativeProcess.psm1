Set-StrictMode -Version Latest

function ConvertTo-WebNativeArgument {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $builder = New-Object Text.StringBuilder
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }
        if ($character -eq '"') {
            [void]$builder.Append(('\' * (($backslashes * 2) + 1)))
            [void]$builder.Append('"')
            $backslashes = 0
            continue
        }
        if ($backslashes -gt 0) {
            [void]$builder.Append(('\' * $backslashes))
            $backslashes = 0
        }
        [void]$builder.Append($character)
    }
    if ($backslashes -gt 0) {
        [void]$builder.Append(('\' * ($backslashes * 2)))
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Stop-WebNativeProcessTree {
    param([Parameter(Mandatory)][int]$ProcessId)

    if ($ProcessId -eq $PID) {
        throw 'Refusing to stop the current PowerShell process.'
    }

    $processes = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
    $orderedIds = @($ProcessId)
    for ($index = 0; $index -lt $orderedIds.Count; $index++) {
        $parentId = $orderedIds[$index]
        $orderedIds += @(
            $processes |
                Where-Object { [int]$_.ParentProcessId -eq $parentId } |
                ForEach-Object { [int]$_.ProcessId }
        )
    }

    for ($index = $orderedIds.Count - 1; $index -ge 0; $index--) {
        Stop-Process -Id $orderedIds[$index] -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-WebNativeProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$ArgumentList,
        [ValidateRange(1, 86400)][int]$TimeoutSeconds = 60,
        [string]$WorkingDirectory,
        [hashtable]$Environment = @{}
    )

    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = (($ArgumentList | ForEach-Object { ConvertTo-WebNativeArgument $_ }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    if ($WorkingDirectory) {
        $startInfo.WorkingDirectory = $WorkingDirectory
    }
    foreach ($entry in $Environment.GetEnumerator()) {
        $startInfo.EnvironmentVariables[[string]$entry.Key] = [string]$entry.Value
    }

    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    $stdoutTask = $null
    $stderrTask = $null
    $timedOut = $false
    $processId = $null
    try {
        if (-not $process.Start()) {
            throw "Native process did not start: $FilePath"
        }
        $processId = $process.Id
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            Stop-WebNativeProcessTree -ProcessId $processId
            if (-not $process.WaitForExit(5000)) {
                $process.Kill()
                $process.WaitForExit()
            }
        } else {
            # Flush asynchronous output callbacks before reading the tasks.
            $process.WaitForExit()
        }

        $stdout = if ($stdoutTask) { $stdoutTask.GetAwaiter().GetResult() } else { '' }
        $stderr = if ($stderrTask) { $stderrTask.GetAwaiter().GetResult() } else { '' }
        return [pscustomobject]@{
            ProcessId = $processId
            ExitCode = if ($process.HasExited) { $process.ExitCode } else { $null }
            TimedOut = $timedOut
            StdOut = [string]$stdout
            StdErr = [string]$stderr
        }
    } finally {
        if ($processId -and -not $process.HasExited) {
            Stop-WebNativeProcessTree -ProcessId $processId
        }
        $process.Dispose()
    }
}

Export-ModuleMember -Function Invoke-WebNativeProcess
