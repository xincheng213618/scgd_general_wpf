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
$transportModuleText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'RemotePowerShellTransport.psm1') -Raw
Assert-Equal $true $loaderText.Contains('[Console]::In.ReadLine()') 'Transport loader does not consume a complete line.'
Assert-Equal $false $loaderText.Contains('[Console]::In.ReadToEnd()') 'Transport loader still waits for stdin EOF.'
Assert-Equal $true $transportModuleText.Contains('$process.StandardInput.Close()') 'Transport client does not close stdin after the payload line.'
$payloadLines = @($transport.stdin_payload.TrimEnd("`r", "`n") -split '\r?\n')
Assert-Equal $true ($payloadLines.Count -gt 2) 'Transport payload was not split into bounded chunks.'
Assert-Equal '__COLORVISION_REMOTE_POWERSHELL_PAYLOAD_END__' $payloadLines[-1] 'Transport payload terminator changed.'
Assert-Equal $true (@($payloadLines | Where-Object { $_.Length -gt 4096 }).Count -eq 0) 'Transport payload contains an oversized line.'

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
    loader = 'chunked-ReadLine'
    stdin_closed_after_write = $true
    payload_chunks = $payloadLines.Count - 1
    payload_characters = $transport.stdin_payload.Length
    explicit_process_pipe = $true
} | ConvertTo-Json -Compress
