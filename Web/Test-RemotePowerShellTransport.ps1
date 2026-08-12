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

Import-Module (Join-Path $PSScriptRoot 'RemotePowerShellTransport.psm1') -Force
$scriptText = @'
# Exercise a payload larger than the deployment script that exposed the native
# PowerShell pipeline stall. The padding remains a valid PowerShell comment.
# __PADDING__
$unicode = [string][char]0x90E8 + [char]0x7F72 + [char]0x901A + [char]0x9053
[ordered]@{
    status = 'success'
    unicode_base64 = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($unicode))
} | ConvertTo-Json -Compress
'@
$scriptText = $scriptText.Replace('__PADDING__', ('x' * 80000))
$expectedUnicode = [string][char]0x90E8 + [char]0x7F72 + [char]0x901A + [char]0x9053
$expectedUnicodeBase64 = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($expectedUnicode))
$transport = New-WebRemotePowerShellTransport -ScriptText $scriptText
$loaderText = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($transport.encoded_loader))
Assert-Equal $true $loaderText.Contains('[Console]::In.ReadLine()') 'Transport loader does not consume a complete line.'
Assert-Equal $false $loaderText.Contains('[Console]::In.ReadToEnd()') 'Transport loader still waits for stdin EOF.'

$result = Invoke-WebRemotePowerShellProcess `
    -FilePath 'powershell.exe' `
    -ArgumentList @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $transport.encoded_loader) `
    -StandardInput $transport.stdin_payload `
    -TimeoutMilliseconds 5000
Assert-Equal 0 $result.exit_code 'Transport child failed.'
$payload = $result.stdout | ConvertFrom-Json
Assert-Equal 'success' $payload.status 'Transport payload did not execute.'
Assert-Equal $expectedUnicodeBase64 $payload.unicode_base64 'Transport payload changed Unicode content.'

[ordered]@{
    status = 'success'
    loader = 'ReadLine'
    stdin_left_open = $true
    payload_characters = $transport.stdin_payload.Length
    explicit_process_pipe = $true
} | ConvertTo-Json -Compress
