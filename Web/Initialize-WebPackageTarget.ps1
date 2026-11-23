[CmdletBinding()]
param(
    [switch]$Test,
    [string]$ActivateZip,
    [string]$Sha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$storage = 'D:\ColorVision'
$legacyRepo = 'C:\Users\Administrator\Desktop\scgd_general_wpf'
$legacyData = Join-Path $legacyRepo 'Web\Backend'
$taskPath = '\ColorVision\'
$pythonExe = 'C:\Users\Administrator\AppData\Local\Programs\Python\Python310\python.exe'
$legacyTask = Get-ScheduledTask -TaskPath $taskPath -TaskName 'ColorVisionWeb'
$root = Join-Path $storage $(if ($Test) { 'web-package-test' } else { 'web-package' })
$taskName = if ($Test) { 'ColorVisionWebPackageTest' } else { 'ColorVisionWeb' }
$port = if ($Test) { 19998 } else { 9998 }
$dataDir = if ($Test) { Join-Path $root 'data' } else { $legacyData }
$docsDir = Join-Path $legacyRepo 'docs'
$launcher = Join-Path $root 'Start-WebPackage.ps1'
$installer = Join-Path $root 'Install-WebPackage.ps1'

foreach ($file in @($launcher, $installer, $pythonExe)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Required file is missing: $file" }
}

function New-WebTaskAction {
    $argument = '-NoLogo -NoProfile -NonInteractive -File "{0}" -Root "{1}" -Port {2} -PythonExe "{3}" -DataDir "{4}" -DocsDir "{5}"' -f
        $launcher, $root, $port, $pythonExe, $dataDir, $docsDir
    New-ScheduledTaskAction -Execute (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -Argument $argument -WorkingDirectory $root
}

function Get-WebPortOwner {
    $listeners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    $owners = @($listeners | ForEach-Object OwningProcess | Sort-Object -Unique)
    if ($owners.Count -gt 1) { throw "More than one process listens on port $port." }
    if ($owners.Count -eq 1) { return [int]$owners[0] }
}

function Wait-WebPortClosed {
    $timeout = [Diagnostics.Stopwatch]::StartNew()
    do {
        if (-not (Get-WebPortOwner)) { return }
        Start-Sleep -Milliseconds 250
    } while ($timeout.Elapsed.TotalSeconds -lt 15)
    throw "Port $port did not close."
}

function Write-NewJson([string]$Path, [object]$Value) {
    $file = [IO.File]::Open($Path, [IO.FileMode]::CreateNew)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 5))
        $file.Write($bytes, 0, $bytes.Length)
    } finally { $file.Dispose() }
}

$targetPath = Join-Path $root 'target.json'
if ($ActivateZip) {
    if ($Test -or $Sha256 -notmatch '\A[0-9a-fA-F]{64}\z') { throw 'First production activation requires a staged package and its SHA-256.' }
    if (-not (Test-Path -LiteralPath $targetPath) -or (Test-Path -LiteralPath (Join-Path $root 'active.json'))) {
        throw 'First package activation requires a prepared production target with no active package.'
    }
    $archiveItem = Get-Item -LiteralPath $ActivateZip
    $incoming = Join-Path $root 'incoming'
    if ($archiveItem.DirectoryName -ine $incoming -or $archiveItem.Name -notmatch '^[0-9a-f]{32}\.zip$' -or
        ($archiveItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        (Get-FileHash -LiteralPath $archiveItem.FullName -Algorithm SHA256).Hash -ne $Sha256) {
        throw 'The incoming production ZIP does not match the staged artifact.'
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archiveItem.FullName)
    try {
        $entry = $archive.GetEntry('manifest.json')
        if (-not $entry -or $entry.Length -gt 4MB) { throw 'The staged package manifest is missing or invalid.' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } finally { $archive.Dispose() }
    if ($manifest.releaseId -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$' -or
        $manifest.backendRequirementsSha256 -notmatch '^[a-fA-F0-9]{64}$') {
        throw 'The staged package identifiers are invalid.'
    }
    $stagedRelease = Join-Path (Join-Path $root 'releases') $manifest.releaseId
    $stagedDependencies = Join-Path (Join-Path $root 'dependencies') $manifest.backendRequirementsSha256
    if (-not (Test-Path -LiteralPath (Join-Path $stagedRelease 'manifest.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $stagedDependencies '.colorvision-python-runtime') -PathType Leaf)) {
        throw 'Verify and stage the first package before production activation.'
    }
    $legacyAction = @($legacyTask.Actions)
    if ($legacyAction.Count -ne 1 -or $legacyAction[0].Execute.Trim('"') -ine $pythonExe -or
        $legacyAction[0].WorkingDirectory -ine $legacyData -or
        $legacyAction[0].Arguments -notmatch '^(?:"[^"]*app\.py"|[^"]*app\.py)\s+--port\s+9998\s*$') {
        throw 'The existing production task action did not match the expected Python launcher.'
    }
    $target = Get-Content -LiteralPath $targetPath -Raw | ConvertFrom-Json
    $installArguments = @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $installer,
        '-Root', $root, '-ZipPath', $ActivateZip, '-Sha256', $Sha256, '-TaskName', $taskName, '-TaskPath', $taskPath,
        '-DataDir', $dataDir, '-Port', [string]$port, '-PythonExe', $pythonExe, '-HistoryPath', [string]$target.historyPath
    )
    $oldPid = Get-WebPortOwner
    $oldProcess = if ($oldPid) { Get-CimInstance Win32_Process -Filter "ProcessId = $oldPid" }
    if (-not $oldProcess -or $oldProcess.ExecutablePath -ine $pythonExe -or
        $oldProcess.CommandLine -notmatch 'app\.py"?\s+--port\s+9998\s*$') {
        throw 'The production listener could not be attributed to the legacy Web task.'
    }
    $ready = Invoke-RestMethod "http://127.0.0.1:$port/api/ready" -TimeoutSec 5
    if ($ready.ready -ne $true) { throw 'The existing production service is not ready.' }
    $actionBackup = [ordered]@{
        execute = $legacyAction[0].Execute; arguments = $legacyAction[0].Arguments; workingDirectory = $legacyAction[0].WorkingDirectory
    }
    $backupName = 'legacy-action-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff') + '.json'
    Write-NewJson (Join-Path $root $backupName) $actionBackup
    $cutover = [Diagnostics.Stopwatch]::StartNew()
    try {
        Stop-ScheduledTask -TaskPath $taskPath -TaskName $taskName
        Wait-WebPortClosed
        Set-ScheduledTask -TaskPath $taskPath -TaskName $taskName -Action (New-WebTaskAction) | Out-Null
        $result = & (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') @installArguments
        if ($LASTEXITCODE -ne 0) { throw "Package installer failed: $result" }
        [pscustomobject]@{
            oldPid = $oldPid
            cutoverSeconds = [Math]::Round($cutover.Elapsed.TotalSeconds, 2)
            deployment = ($result | ConvertFrom-Json)
        }
    } catch {
        $failure = $_.Exception.Message
        try {
            Stop-ScheduledTask -TaskPath $taskPath -TaskName $taskName
            Wait-WebPortClosed
            $restore = New-ScheduledTaskAction -Execute $actionBackup.execute -Argument $actionBackup.arguments -WorkingDirectory $actionBackup.workingDirectory
            Set-ScheduledTask -TaskPath $taskPath -TaskName $taskName -Action $restore | Out-Null
            Start-ScheduledTask -TaskPath $taskPath -TaskName $taskName
            $timeout = [Diagnostics.Stopwatch]::StartNew()
            do {
                $restored = $false
                try { $restored = (Invoke-RestMethod "http://127.0.0.1:$port/api/ready" -TimeoutSec 3).ready -eq $true }
                catch { }
                if ($restored) { break }
                Start-Sleep -Milliseconds 500
            } while ($timeout.Elapsed.TotalSeconds -lt 45)
            if (-not $restored) { throw 'The legacy service did not become ready.' }
        } catch {
            throw "Package activation failed ($failure). Legacy task recovery failed: $($_.Exception.Message)"
        }
        throw "Package activation failed; the legacy task is ready: $failure"
    }
    return
}

if ($Sha256) { throw 'Sha256 requires ActivateZip.' }
if (Test-Path -LiteralPath $targetPath) { throw "The package target is already initialized: $root" }
New-Item -ItemType Directory -Path (Join-Path $root 'incoming') -Force | Out-Null
if ($Test) {
    if ((Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName -ErrorAction SilentlyContinue) -or (Get-WebPortOwner)) {
        throw 'The test task or port is already in use.'
    }
    $testStorage = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Path $dataDir, $testStorage -Force | Out-Null
    Write-NewJson (Join-Path $dataDir 'config.json') @{
        storage_path = $testStorage
        secret_key = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
        host = '127.0.0.1'; port = $port; debug = $false; scheduler_enabled = $false
        upload_auth = @{ username = 'web-package-test'; password = [Guid]::NewGuid().ToString('N') }
    }
    & $pythonExe -c 'import sqlite3,sys; sqlite3.connect(sys.argv[1]).close()' (Join-Path $dataDir 'marketplace.db')
    if ($LASTEXITCODE -ne 0) { throw 'The test database could not be initialized.' }
    Register-ScheduledTask -TaskPath $taskPath -TaskName $taskName -Action (New-WebTaskAction) -Principal $legacyTask.Principal |
        Out-Null
} else {
    foreach ($name in 'config.json', 'marketplace.db') {
        if (-not (Test-Path -LiteralPath (Join-Path $dataDir $name) -PathType Leaf)) { throw "Production data file is missing: $name" }
    }
}
Write-NewJson $targetPath @{
    taskName = $taskName; taskPath = $taskPath; dataDir = $dataDir; pythonExe = $pythonExe; port = $port
    historyPath = if ($Test) { '' } else { Join-Path $storage 'web-deploy-history.jsonl' }
}
[pscustomobject]@{ status = 'prepared'; root = $root; taskName = $taskName; port = $port }
