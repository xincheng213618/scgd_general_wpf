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

Export-ModuleMember -Function New-WebRemotePowerShellTransport
