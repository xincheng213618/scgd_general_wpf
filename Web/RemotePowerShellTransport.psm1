Set-StrictMode -Version Latest

$script:RemotePayloadTerminator = '__COLORVISION_REMOTE_POWERSHELL_PAYLOAD_END__'
$script:RemotePayloadChunkSize = 4096

function New-WebRemotePowerShellTransport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ScriptText
    )

    $payload = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($ScriptText))
    $payloadChunks = @()
    for ($offset = 0; $offset -lt $payload.Length; $offset += $script:RemotePayloadChunkSize) {
        $length = [Math]::Min($script:RemotePayloadChunkSize, $payload.Length - $offset)
        $payloadChunks += $payload.Substring($offset, $length)
    }
    $loader = @'
$global:ProgressPreference = 'SilentlyContinue'
$payloadBuilder = New-Object Text.StringBuilder
$payloadComplete = $false
while (($payloadLine = [Console]::In.ReadLine()) -ne $null) {
    if ($payloadLine -eq '__COLORVISION_REMOTE_POWERSHELL_PAYLOAD_END__') {
        $payloadComplete = $true
        break
    }
    [void]$payloadBuilder.Append([regex]::Replace($payloadLine, '[^A-Za-z0-9+/=]', ''))
}
if (-not $payloadComplete) {
    throw 'Remote PowerShell payload ended before its terminator.'
}
$payload = $payloadBuilder.ToString()
if ([string]::IsNullOrWhiteSpace($payload)) {
    throw 'Remote PowerShell payload was empty after transport decoding.'
}
$scriptText = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($payload))
$scriptBlock = [ScriptBlock]::Create($scriptText)
& $scriptBlock
'@

    # Windows PowerShell 5.1 can consume the first character from redirected
    # stdin while starting powershell.exe. A sacrificial blank line preserves
    # the Base64 payload; the loader ignores it, and FromBase64String permits
    # whitespace between Base64 characters.
    return [pscustomobject]@{
        stdin_payload = "`n" + (($payloadChunks + $script:RemotePayloadTerminator) -join "`n") + "`n"
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

    $inputLines = @($StandardInput.TrimEnd("`r", "`n") -split '\r?\n')
    if ($inputLines.Count -lt 2 -or $inputLines[-1] -ne $script:RemotePayloadTerminator) {
        throw 'Remote PowerShell transport input must end with its payload terminator.'
    }
    $oversizedLine = @($inputLines | Where-Object { $_.Length -gt $script:RemotePayloadChunkSize } | Select-Object -First 1)
    if ($oversizedLine.Count -gt 0) {
        throw "Remote PowerShell transport chunk exceeds $($script:RemotePayloadChunkSize) characters."
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
        $process.StandardInput.Write($StandardInput)
        $process.StandardInput.Flush()
        # Each bounded line lets the remote loader consume data while OpenSSH
        # continues forwarding the remaining payload. Close after the explicit
        # terminator so the transport cannot leave a writer behind.
        $process.StandardInput.Close()

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
