[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(ParameterSetName = 'Build')][string]$Revision = 'HEAD',
    [Parameter(ParameterSetName = 'Build')][string]$SourceCommit,
    [Parameter(ParameterSetName = 'Package', Mandatory)][string]$PackagePath,
    [Parameter(ParameterSetName = 'Rollback', Mandatory)][switch]$Rollback,
    [Parameter(ParameterSetName = 'Package')][switch]$StageOnly,
    [switch]$Test,
    [string]$SshTarget = 'cv-publish'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$total = [Diagnostics.Stopwatch]::StartNew()
$buildSeconds = $null
$uploadSeconds = $null
$package = $null
$remotePath = $null
$expectedHash = $null
$targetRoot = if ($Test) { 'D:\ColorVision\web-package-test' } else { 'D:\ColorVision\web-package' }
Import-Module (Join-Path $PSScriptRoot 'RemotePowerShellTransport.psm1') -Force

function Quote-PowerShellLiteral([string]$Value) {
    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-WebPackageRemote([string]$ScriptText) {
    $transport = New-WebRemotePowerShellTransport -ScriptText $ScriptText
    $result = Invoke-WebRemotePowerShellProcess -FilePath 'ssh' -ArgumentList @(
        '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10', $SshTarget,
        'powershell.exe', '-NoProfile', '-NonInteractive', '-EncodedCommand', $transport.encoded_loader
    ) -StandardInput $transport.stdin_payload
    if ($result.exit_code -ne 0) {
        if ($result.stdout) { Write-Host $result.stdout.TrimEnd() }
        throw "NAS command failed with exit code $($result.exit_code). $($result.stderr.Trim())"
    }
    return $result.stdout
}

if ($PSCmdlet.ParameterSetName -eq 'Build') {
    $build = [Diagnostics.Stopwatch]::StartNew()
    $buildOptions = @{ Revision = $Revision }
    if ($SourceCommit) { $buildOptions.SourceCommit = $SourceCommit }
    $artifact = & (Join-Path $PSScriptRoot 'Build-WebPackage.ps1') @buildOptions
    if (-not $artifact) { throw 'The Web package build returned no artifact.' }
    $PackagePath = $artifact.zipPath
    $buildSeconds = [Math]::Round($build.Elapsed.TotalSeconds, 2)
}

if (-not $Rollback) {
    $package = (Resolve-Path -LiteralPath $PackagePath).ProviderPath
    if ([IO.Path]::GetExtension($package) -ne '.zip' -or -not (Test-Path -LiteralPath $package -PathType Leaf)) {
        throw 'Choose a Web release ZIP.'
    }
    $expectedHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    $fileName = [Guid]::NewGuid().ToString('N') + '.zip'
    $remotePath = Join-Path (Join-Path $targetRoot 'incoming') $fileName
    $uploadTarget = $SshTarget + ':' + $remotePath.Replace('\', '/')
    $upload = [Diagnostics.Stopwatch]::StartNew()
    & scp -o BatchMode=yes -o ConnectTimeout=10 $package $uploadTarget
    if ($LASTEXITCODE -ne 0) { throw "Could not upload the Web release ZIP to $SshTarget." }
    $uploadSeconds = [Math]::Round($upload.Elapsed.TotalSeconds, 2)
}

$remoteTemplate = @'
$ErrorActionPreference = 'Stop'
$root = __TARGET_ROOT__
$target = Get-Content -LiteralPath (Join-Path $root 'target.json') -Raw | ConvertFrom-Json
$arguments = @{
    Root = $root
    TaskName = [string]$target.taskName
    TaskPath = [string]$target.taskPath
    DataDir = [string]$target.dataDir
    PythonExe = [string]$target.pythonExe
    Port = [int]$target.port
}
if ($target.historyPath) { $arguments.HistoryPath = [string]$target.historyPath }
__OPERATION__
$worker = @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', (Join-Path $root 'Install-WebPackage.ps1'))
foreach ($key in $arguments.Keys) {
    $worker += "-$key"
    if ($arguments[$key] -isnot [bool]) { $worker += [string]$arguments[$key] }
}
& (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') @worker
exit $LASTEXITCODE
'@
if ($Rollback) {
    $operation = '$arguments.Rollback = $true'
} else {
    $operation = '$arguments.ZipPath = ' + (Quote-PowerShellLiteral $remotePath) + [Environment]::NewLine +
        '$arguments.Sha256 = ' + (Quote-PowerShellLiteral $expectedHash)
    if ($StageOnly) { $operation += [Environment]::NewLine + '$arguments.StageOnly = $true' }
}
$remoteScript = $remoteTemplate.Replace('__TARGET_ROOT__', (Quote-PowerShellLiteral $targetRoot)).Replace('__OPERATION__', $operation)
$apply = [Diagnostics.Stopwatch]::StartNew()
$rawResult = Invoke-WebPackageRemote $remoteScript
$nasResult = $rawResult | ConvertFrom-Json
[pscustomobject]@{
    target = if ($Test) { 'test' } else { 'production' }
    packagePath = $package
    remotePackagePath = $remotePath
    sha256 = $expectedHash
    buildSeconds = $buildSeconds
    uploadSeconds = $uploadSeconds
    nasSeconds = [Math]::Round($apply.Elapsed.TotalSeconds, 2)
    totalSeconds = [Math]::Round($total.Elapsed.TotalSeconds, 2)
    deployment = $nasResult
}
