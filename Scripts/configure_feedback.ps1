#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://xc213618.ddns.me:9998',
    [Security.SecureString]$ApiKey,
    [switch]$AllowInsecureHttp
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'On non-Windows hosts, set COLORVISION_FEEDBACK_API_KEY and COLORVISION_FEEDBACK_BASE_URL in your user environment.' }
$uri = [Uri]$BaseUrl
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -notin @('http', 'https') -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) {
    throw 'Provide an HTTP(S) service URL without credentials, query or fragment.'
}
if ($uri.Scheme -eq 'http' -and -not $AllowInsecureHttp) { throw 'The current HTTP endpoint requires explicit -AllowInsecureHttp.' }
if ($null -eq $ApiKey) { $ApiKey = Read-Host 'Feedback read-only API key (input hidden)' -AsSecureString }
if ($ApiKey.Length -eq 0) { throw 'An API key is required.' }
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($ApiKey)
try {
    [Environment]::SetEnvironmentVariable('COLORVISION_FEEDBACK_API_KEY', [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer), 'User')
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
}
[Environment]::SetEnvironmentVariable('COLORVISION_FEEDBACK_BASE_URL', $uri.AbsoluteUri.TrimEnd('/'), 'User')
[Environment]::SetEnvironmentVariable('COLORVISION_FEEDBACK_ALLOW_HTTP', $(if ($AllowInsecureHttp) { '1' } else { $null }), 'User')
Write-Output 'Feedback access configured for the current Windows user. The API key is not stored in the repository.'
