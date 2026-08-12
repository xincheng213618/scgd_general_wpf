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

function Invoke-TransportChild {
    param(
        [Parameter(Mandatory)][string]$EncodedLoader,
        [Parameter(Mandatory)][string]$StandardInput
    )

    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'powershell.exe'
    $startInfo.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $EncodedLoader"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw 'Transport test child did not start.'
        }
        $process.StandardInput.WriteLine($StandardInput)
        $process.StandardInput.Flush()
        # Deliberately leave stdin open. The loader must act on the complete
        # single-line payload instead of waiting for an EOF that SSH may delay.
        if (-not $process.WaitForExit(5000)) {
            $process.Kill()
            throw 'Transport loader waited for stdin EOF instead of consuming one payload line.'
        }
        return [pscustomobject]@{
            exit_code = $process.ExitCode
            stdout = $process.StandardOutput.ReadToEnd()
            stderr = $process.StandardError.ReadToEnd()
        }
    } finally {
        if (-not $process.HasExited) {
            $process.Kill()
        }
        $process.StandardInput.Dispose()
        $process.Dispose()
    }
}

Import-Module (Join-Path $PSScriptRoot 'RemotePowerShellTransport.psm1') -Force
$scriptText = @'
$unicode = [string][char]0x90E8 + [char]0x7F72 + [char]0x901A + [char]0x9053
[ordered]@{
    status = 'success'
    unicode_base64 = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($unicode))
} | ConvertTo-Json -Compress
'@
$expectedUnicode = [string][char]0x90E8 + [char]0x7F72 + [char]0x901A + [char]0x9053
$expectedUnicodeBase64 = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($expectedUnicode))
$transport = New-WebRemotePowerShellTransport -ScriptText $scriptText
$loaderText = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($transport.encoded_loader))
Assert-Equal $true $loaderText.Contains('[Console]::In.ReadLine()') 'Transport loader does not consume a complete line.'
Assert-Equal $false $loaderText.Contains('[Console]::In.ReadToEnd()') 'Transport loader still waits for stdin EOF.'

$result = Invoke-TransportChild -EncodedLoader $transport.encoded_loader -StandardInput $transport.stdin_payload
Assert-Equal 0 $result.exit_code 'Transport child failed.'
$payload = $result.stdout | ConvertFrom-Json
Assert-Equal 'success' $payload.status 'Transport payload did not execute.'
Assert-Equal $expectedUnicodeBase64 $payload.unicode_base64 'Transport payload changed Unicode content.'

[ordered]@{
    status = 'success'
    loader = 'ReadLine'
    stdin_left_open = $true
    payload_characters = $transport.stdin_payload.Length
} | ConvertTo-Json -Compress
