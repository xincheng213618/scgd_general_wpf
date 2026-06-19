param(
    [Parameter(Mandatory = $true)]
    [string]$InputJsonPath
)

$ErrorActionPreference = 'Stop'
$data = Get-Content -LiteralPath $InputJsonPath -Raw | ConvertFrom-Json
$appDirectory = [string]$data.appDirectory
$thumbnailCacheDirectory = [string]$data.thumbnailCacheDirectory

if ([string]::IsNullOrWhiteSpace($appDirectory)) {
    throw "Application directory was not provided."
}

$comHostDll = Join-Path $appDirectory 'ColorVision.ShellExtension.comhost.dll'
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

function Remove-RegistryKeyTree {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = Resolve-RegistryPath $Path
    $parentPath = Split-Path -Parent $resolved.SubKey
    $leafName = Split-Path -Leaf $resolved.SubKey
    $parentKey = $resolved.Root.OpenSubKey($parentPath, $true)
    if ($null -eq $parentKey) {
        return
    }

    try {
        $parentKey.DeleteSubKeyTree($leafName, $false)
    }
    finally {
        $parentKey.Dispose()
    }
}

function Remove-RegistryValue {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Name
    )

    $resolved = Resolve-RegistryPath $Path
    $key = $resolved.Root.OpenSubKey($resolved.SubKey, $true)
    if ($null -eq $key) {
        return
    }

    try {
        $key.DeleteValue($Name, $false)
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

foreach ($extension in $handlers.Keys) {
    Remove-RegistryKeyTree "HKCR\$extension\ShellEx\$thumbnailProviderIid"
}

foreach ($clsid in $handlers.Values) {
    Remove-RegistryValue $approvedKey $clsid
}

if (Test-Path -LiteralPath $comHostDll) {
    Invoke-ExternalProcess 'regsvr32.exe' @('/s', '/u', $comHostDll) 20
}

if (-not [string]::IsNullOrWhiteSpace($thumbnailCacheDirectory) -and (Test-Path -LiteralPath $thumbnailCacheDirectory -PathType Container)) {
    Get-ChildItem -LiteralPath $thumbnailCacheDirectory -Filter 'thumbcache_*.db' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath $thumbnailCacheDirectory -Filter 'iconcache_*.db' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

Write-Output "Thumbnail shell extension unregistered: $comHostDll"
