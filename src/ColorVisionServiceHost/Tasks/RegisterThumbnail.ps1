param(
    [Parameter(Mandatory = $true)]
    [string]$InputJsonPath
)

$ErrorActionPreference = 'Stop'
$data = Get-Content -LiteralPath $InputJsonPath -Raw | ConvertFrom-Json
$appDirectory = [string]$data.appDirectory
$thumbnailCacheDirectory = [string]$data.thumbnailCacheDirectory

if ([string]::IsNullOrWhiteSpace($appDirectory) -or -not (Test-Path -LiteralPath $appDirectory -PathType Container)) {
    throw "Application directory was not found: $appDirectory"
}

$comHostDll = Join-Path $appDirectory 'ColorVision.ShellExtension.comhost.dll'
if (-not (Test-Path -LiteralPath $comHostDll)) {
    throw "Shell extension was not found: $comHostDll"
}

$thumbnailProviderIid = '{E357FCCD-A995-4576-B01F-234630154E96}'
$handlers = @{
    '.cvraw' = '{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}'
    '.cvcie' = '{8C6F3B4D-9E2A-5F7B-C3D4-2E4F6A8B9C0D}'
}
$approvedKey = 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved'

function Resolve-RegistryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ($Path -match '^HKCR\\(.+)$') {
        return @{ Root = [Microsoft.Win32.Registry]::ClassesRoot; SubKey = $Matches[1] }
    }
    if ($Path -match '^HKLM\\(.+)$') {
        return @{ Root = [Microsoft.Win32.Registry]::LocalMachine; SubKey = $Matches[1] }
    }
    throw "Unsupported registry path: $Path"
}

function Set-RegistryString {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Name,
        [AllowEmptyString()][string]$Value = ''
    )

    $resolved = Resolve-RegistryPath $Path
    $key = $resolved.Root.CreateSubKey($resolved.SubKey)
    if ($null -eq $key) {
        throw "Unable to create registry key: $Path"
    }

    try {
        $key.SetValue($Name, $Value, [Microsoft.Win32.RegistryValueKind]::String)
    }
    finally {
        $key.Dispose()
    }
}

function Join-CommandArguments {
    param([string[]]$Arguments)

    return (($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_.Replace('\', '\\').Replace('"', '\"')) + '"'
        }
        else {
            $_
        }
    }) -join ' ')
}

function Invoke-ExternalProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 20
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FileName
    $startInfo.Arguments = Join-CommandArguments $Arguments
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Failed to start process: $FileName"
    }

    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            throw "$FileName timed out."
        }
        if ($process.ExitCode -ne 0) {
            throw "$FileName failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }
}

Invoke-ExternalProcess 'regsvr32.exe' @('/s', $comHostDll) 20

foreach ($extension in $handlers.Keys) {
    Set-RegistryString "HKCR\$extension\ShellEx\$thumbnailProviderIid" '' $handlers[$extension]
}

foreach ($clsid in $handlers.Values) {
    Set-RegistryString $approvedKey $clsid 'ColorVision Thumbnail Handler'
}

if (-not [string]::IsNullOrWhiteSpace($thumbnailCacheDirectory) -and (Test-Path -LiteralPath $thumbnailCacheDirectory -PathType Container)) {
    Get-ChildItem -LiteralPath $thumbnailCacheDirectory -Filter 'thumbcache_*.db' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath $thumbnailCacheDirectory -Filter 'iconcache_*.db' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

Write-Output "Thumbnail shell extension registered: $comHostDll"
