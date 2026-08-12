Set-StrictMode -Version Latest

function New-WebRemotePowerShellTransport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ScriptText
    )

    $payload = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($ScriptText))
    $loader = @'
$global:ProgressPreference = 'SilentlyContinue'
$payloadLine = [Console]::In.ReadLine()
if ([string]::IsNullOrWhiteSpace($payloadLine)) {
    throw 'Remote PowerShell payload was not received.'
}
$payload = [regex]::Replace($payloadLine, '[^A-Za-z0-9+/=]', '')
if ([string]::IsNullOrWhiteSpace($payload)) {
    throw 'Remote PowerShell payload was empty after transport decoding.'
}
$scriptText = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($payload))
$scriptBlock = [ScriptBlock]::Create($scriptText)
& $scriptBlock
'@

    return [pscustomobject]@{
        stdin_payload = '!' + $payload
        encoded_loader = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($loader))
    }
}

function Invoke-WebRemotePowerShellProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$FilePath,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$ArgumentList,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StandardInput,
        [ValidateRange(0, 2147483647)][int]$TimeoutMilliseconds = 0
    )

    if ($StandardInput -match '[\r\n]') {
        throw 'Remote PowerShell transport input must contain exactly one line.'
    }
    foreach ($argument in $ArgumentList) {
        if ([string]::IsNullOrWhiteSpace($argument) -or $argument -match '[\s"]') {
            throw "Remote PowerShell process arguments cannot contain whitespace or quotes: $argument"
        }
    }

    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = [string]::Join(' ', $ArgumentList)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    $started = $false
    try {
        if (-not $process.Start()) {
            throw "Remote PowerShell process did not start: $FilePath"
        }
        $started = $true
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.WriteLine($StandardInput)
        $process.StandardInput.Flush()

        if ($TimeoutMilliseconds -gt 0) {
            if (-not $process.WaitForExit($TimeoutMilliseconds)) {
                throw "Remote PowerShell process timed out after $TimeoutMilliseconds milliseconds."
            }
        } else {
            $process.WaitForExit()
        }

        return [pscustomobject]@{
            exit_code = $process.ExitCode
            stdout = $stdoutTask.GetAwaiter().GetResult()
            stderr = $stderrTask.GetAwaiter().GetResult()
        }
    } finally {
        if ($started) {
            if (-not $process.HasExited) {
                $process.Kill()
                $process.WaitForExit()
            }
            $process.StandardInput.Dispose()
        }
        $process.Dispose()
    }
}

Export-ModuleMember -Function New-WebRemotePowerShellTransport, Invoke-WebRemotePowerShellProcess
