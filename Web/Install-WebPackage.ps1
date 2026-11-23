[CmdletBinding(DefaultParameterSetName = 'Apply')]
param(
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory, ParameterSetName = 'Apply')][string]$ZipPath,
    [Parameter(Mandatory, ParameterSetName = 'Apply')][ValidatePattern('^[0-9A-Fa-f]{64}\z')][string]$Sha256,
    [Parameter(Mandatory, ParameterSetName = 'Rollback')][switch]$Rollback,
    [Parameter(ParameterSetName = 'Apply')][switch]$StageOnly,
    [Parameter(Mandatory)][string]$TaskName,
    [Parameter(Mandatory)][string]$TaskPath,
    [Parameter(Mandatory)][ValidateRange(1, 65535)][int]$Port,
    [Parameter(Mandatory)][string]$PythonExe,
    [string]$DataDir,
    [string]$HistoryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding($false, $true)

function Assert-PlainItem {
    param([string]$Path, [switch]$Directory)
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSIsContainer -ne $Directory.IsPresent -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Unexpected file type or reparse point: $Path"
    }
    return $item
}

function Assert-SafePart {
    param([string]$Name)
    if (-not $Name -or $Name -match '[\x00-\x1f<>:"/\\|?*]|[ .]$|^(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])(?:\.|$)') {
        throw "Unsafe package path component: $Name"
    }
}

function Assert-ReleaseId {
    param($Value)
    if ($Value -isnot [string] -or $Value -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z') {
        throw 'Invalid releaseId.'
    }
    Assert-SafePart $Value
}

function Assert-PackagePath {
    param([string]$Path, [switch]$Directory)
    $parts = $Path.Split('/')
    foreach ($part in $parts) { Assert-SafePart $part }
    if (($Directory -and $Path -cin @('Backend', 'Frontend', 'Frontend/dist')) -or
        (-not $Directory -and $Path -ceq 'manifest.json')) { return }
    if ($Path.StartsWith('Backend/', [StringComparison]::Ordinal) -and
        $Path -notmatch '^Backend/(?:config\.json(?:/|$)|marketplace\.db(?:[-./]|$)|storage(?:/|$))') { return }
    if ($Path.StartsWith('Frontend/dist/', [StringComparison]::Ordinal)) { return }
    throw "Unexpected package path: $Path"
}

function Read-PackageManifest {
    param([IO.Stream]$Stream)
    $reader = New-Object IO.StreamReader($Stream, $utf8)
    try { $document = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($document.schemaVersion -ne 1) { throw 'Unsupported package manifest schema.' }
    Assert-ReleaseId $document.releaseId
    if ($document.commit -isnot [string] -or $document.commit -notmatch '^[0-9A-Fa-f]{40}\z' -or
        $document.backendRequirementsSha256 -isnot [string] -or $document.backendRequirementsSha256 -notmatch '^[0-9A-Fa-f]{64}\z' -or
        $document.files -isnot [array]) {
        throw 'Invalid package manifest metadata.'
    }
    $files = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $document.files) {
        if ($file.path -isnot [string] -or $file.path -ieq 'manifest.json' -or
            $file.sha256 -isnot [string] -or $file.sha256 -notmatch '^[0-9A-Fa-f]{64}\z') {
            throw 'Invalid package manifest file record.'
        }
        Assert-PackagePath $file.path
        if ($files.ContainsKey($file.path)) { throw "Duplicate manifest path: $($file.path)" }
        $files.Add($file.path, $file.sha256.ToLowerInvariant())
    }
    foreach ($required in @('Backend/app.py', 'Backend/requirements.txt', 'Frontend/dist/index.html')) {
        if ($required -cnotin $files.Keys) { throw "Required package file missing: $required" }
    }
    $requirements = if ('Backend/requirements-package.txt' -cin $files.Keys) { 'Backend/requirements-package.txt' } else { 'Backend/requirements.txt' }
    if ($files[$requirements] -ne $document.backendRequirementsSha256) { throw 'Requirements digest does not match the manifest file record.' }
    return [pscustomobject]@{
        Id = $document.releaseId
        Commit = $document.commit.ToLowerInvariant()
        RequirementsHash = $document.backendRequirementsSha256.ToLowerInvariant()
        RequirementsFile = $requirements
        Files = $files
    }
}

function Read-Release {
    param([string]$Directory)
    $null = Assert-PlainItem $Directory -Directory
    $manifestFile = Join-Path $Directory 'manifest.json'
    $null = Assert-PlainItem $manifestFile
    $manifest = Read-PackageManifest ([IO.File]::OpenRead($manifestFile))
    if ($manifest.Id -cne [IO.Path]::GetFileName($Directory)) { throw 'Release directory and manifest IDs differ.' }
    return $manifest
}

function Assert-InstalledFiles {
    param([string]$Directory, $Manifest)
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($Directory)
    $found = 0
    while ($pending.Count) {
        foreach ($item in Get-ChildItem -LiteralPath ($pending.Pop()) -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point in release: $($item.FullName)" }
            if ($item.PSIsContainer) { $pending.Push($item.FullName); continue }
            $name = $item.FullName.Substring($Directory.Length + 1).Replace('\', '/')
            if ($name -ceq 'manifest.json') { continue }
            if (-not $Manifest.Files.ContainsKey($name) -or (Get-FileHash -LiteralPath $item.FullName).Hash -ne $Manifest.Files[$name]) {
                throw "Installed release file differs from manifest: $name"
            }
            $found++
        }
    }
    if ($found -ne $Manifest.Files.Count) { throw 'Installed release has missing files.' }
}

function Remove-PrivateStage {
    param([string]$Path, [string]$Parent)
    $full = [IO.Path]::GetFullPath($Path)
    if ([IO.Path]::GetDirectoryName($full) -ne [IO.Path]::GetFullPath($Parent) -or
        [IO.Path]::GetFileName($full) -notmatch '^\.stage-[0-9a-f]{32}$') {
        throw 'Refusing to remove an unexpected staging path.'
    }
    if (Test-Path -LiteralPath $full) {
        $null = Assert-PlainItem $full -Directory
        Remove-Item -LiteralPath $full -Recurse -Force
    }
}

function Publish-StageDirectory {
    param([string]$Path, [string]$Destination)
    $Path = [IO.Path]::GetFullPath($Path)
    $Destination = [IO.Path]::GetFullPath($Destination)
    if ([IO.Path]::GetDirectoryName($Path) -ne [IO.Path]::GetDirectoryName($Destination) -or
        [IO.Path]::GetFileName($Path) -notmatch '^\.stage-[0-9a-f]{32}\z') {
        throw 'Refusing to move an unexpected staging path.'
    }
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        $null = Assert-PlainItem $Path -Directory
        if (Test-Path -LiteralPath $Destination) { throw "Destination already exists: $Destination" }
        try {
            [IO.Directory]::Move($Path, $Destination)
            return
        } catch {
            $cause = $_.Exception
            while ($cause.InnerException) { $cause = $cause.InnerException }
            if ($attempt -eq 8 -or ($cause -isnot [IO.IOException] -and $cause -isnot [UnauthorizedAccessException])) { throw }
            if (Test-Path -LiteralPath $Destination) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}

function Install-ReleaseFiles {
    param([string]$ArchivePath, [string]$ExpectedDigest, [string]$Releases)
    $null = Assert-PlainItem $ArchivePath
    $stream = [IO.File]::Open($ArchivePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $hasher = [Security.Cryptography.SHA256]::Create()
    $archive = $null
    $stage = Join-Path $Releases ('.stage-' + [Guid]::NewGuid().ToString('N'))
    try {
        $digest = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '')
        if ($digest -ne $ExpectedDigest) { throw 'Package SHA-256 does not match the trusted digest.' }
        $stream.Position = 0
        $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Read, $true)
        $entries = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            $isDirectory = $entry.FullName.EndsWith('/')
            $name = if ($isDirectory) { $entry.FullName.Substring(0, $entry.FullName.Length - 1) } else { $entry.FullName }
            Assert-PackagePath $name -Directory:$isDirectory
            if (($entry.ExternalAttributes -band 0x400) -or ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000)) {
                throw "Archive contains a link: $name"
            }
            if ($isDirectory) { continue }
            if ($entries.ContainsKey($name)) { throw "Duplicate archive path: $name" }
            $entries.Add($name, $entry)
        }
        if ('manifest.json' -cnotin $entries.Keys) { throw 'Package manifest is missing.' }
        $manifestEntry = $entries['manifest.json']
        if ($manifestEntry.Length -gt 4MB) { throw 'Package manifest is too large.' }
        $manifest = Read-PackageManifest ($manifestEntry.Open())
        if ($entries.Count -ne $manifest.Files.Count + 1) { throw 'Archive file count differs from manifest.' }
        foreach ($name in $entries.Keys) {
            if ($name -cne 'manifest.json' -and $name -cnotin $manifest.Files.Keys) { throw "Archive contains an unlisted file: $name" }
        }

        $release = Join-Path $Releases $manifest.Id
        $null = [IO.Directory]::CreateDirectory($stage)
        foreach ($name in $entries.Keys) {
            $target = Join-Path $stage $name.Replace('/', '\')
            $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            $inputStream = $entries[$name].Open()
            try {
                $outputStream = [IO.File]::Open($target, [IO.FileMode]::CreateNew)
                try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
            } finally { $inputStream.Dispose() }
            if ($name -cne 'manifest.json' -and (Get-FileHash -LiteralPath $target).Hash -ne $manifest.Files[$name]) {
                throw "Package hash mismatch: $name"
            }
        }
        if (Test-Path -LiteralPath $release) {
            $installed = Read-Release $release
            if ((Get-FileHash -LiteralPath (Join-Path $release 'manifest.json')).Hash -ne
                (Get-FileHash -LiteralPath (Join-Path $stage 'manifest.json')).Hash) {
                throw "Release ID already belongs to a different package: $($manifest.Id)"
            }
            Assert-InstalledFiles $release $installed
        } else {
            Publish-StageDirectory $stage $release
        }
        return $manifest
    } finally {
        if ($archive) { $archive.Dispose() }
        $hasher.Dispose()
        $stream.Dispose()
        Remove-PrivateStage $stage $Releases
    }
}

function Invoke-PythonChecked {
    param([string[]]$Arguments, [int]$TimeoutSeconds = 30)
    $processResult = Invoke-WebNativeProcess -FilePath $PythonExe -ArgumentList $Arguments -TimeoutSeconds $TimeoutSeconds -Environment @{
        PYTHONPATH = ''
        PYTHONNOUSERSITE = '1'
        PYTHONDONTWRITEBYTECODE = '1'
    }
    if ($processResult.TimedOut -or $processResult.ExitCode -ne 0) {
        throw "Python step failed (timeout=$($processResult.TimedOut), exit=$($processResult.ExitCode))."
    }
    return $processResult.StdOut.Trim()
}

function Prepare-Dependencies {
    param($Manifest)
    $runtime = Invoke-PythonChecked @('-c', 'import sys,sysconfig; print(sys.version.split()[0], sysconfig.get_platform(), sys.implementation.cache_tag)')
    $releasePath = Join-Path $releases $Manifest.Id
    $requirements = Join-Path $releasePath $Manifest.RequirementsFile.Replace('/', '\')
    $target = Join-Path $dependencies $Manifest.RequirementsHash
    $marker = '.colorvision-requirements-sha256'
    $runtimeMarker = '.colorvision-python-runtime'
    if (Test-Path -LiteralPath $target) {
        $null = Assert-PlainItem $target -Directory
        $null = Assert-PlainItem (Join-Path $target $marker)
        $null = Assert-PlainItem (Join-Path $target $runtimeMarker)
        if ([IO.File]::ReadAllText((Join-Path $target $marker), $utf8) -ne $Manifest.RequirementsHash -or
            [IO.File]::ReadAllText((Join-Path $target $runtimeMarker), $utf8) -ne $runtime) {
            throw 'Dependency cache does not match the package and Python runtime.'
        }
        return
    }

    $wheelhouse = Join-Path $releasePath 'Backend\wheels'
    $hasWheels = Test-Path -LiteralPath $wheelhouse -PathType Container
    $hasLock = $Manifest.RequirementsFile -ceq 'Backend/requirements-package.txt'
    if ($hasLock -and -not $hasWheels) { throw 'The package is missing its offline Python wheelhouse.' }
    if ($hasWheels) {
        $null = Assert-PlainItem $wheelhouse -Directory
        if (-not (Get-ChildItem -LiteralPath $wheelhouse -File -Filter '*.whl' | Select-Object -First 1)) {
            throw 'The Python wheelhouse is empty.'
        }
    }
    if ($hasLock) {
        foreach ($line in [IO.File]::ReadAllLines($requirements, $utf8)) {
            $entry = $line.Trim()
            if ($entry -and -not $entry.StartsWith('#') -and
                $entry -notmatch '^[A-Za-z0-9_.-]+==[A-Za-z0-9_.!+-]+(?:\s+#.*)?\z') {
                throw 'The Python package lock must list pinned distributions, without direct references or pip flags.'
            }
        }
    }

    $stage = Join-Path $dependencies ('.stage-' + [Guid]::NewGuid().ToString('N'))
    try {
        $null = [IO.Directory]::CreateDirectory($stage)
        $pipArguments = @('-m', 'pip', '--isolated', 'install', '--disable-pip-version-check', '--no-input', '--target', $stage, '-r', $requirements)
        if ($hasWheels) { $pipArguments += @('--no-index', '--find-links', $wheelhouse, '--only-binary=:all:') }
        if ($hasLock) { $pipArguments += '--no-deps' }
        $null = Invoke-PythonChecked $pipArguments -TimeoutSeconds 600
        [IO.File]::WriteAllText((Join-Path $stage $marker), $Manifest.RequirementsHash, $utf8)
        [IO.File]::WriteAllText((Join-Path $stage $runtimeMarker), $runtime, $utf8)
        Publish-StageDirectory $stage $target
    } finally {
        Remove-PrivateStage $stage $dependencies
    }
}

function Test-CandidateBackend {
    param($Manifest)
    $source = Join-Path (Join-Path $releases $Manifest.Id) 'Backend'
    $dependencyPath = Join-Path $dependencies $Manifest.RequirementsHash
    $code = @'
import json, os, shutil, sys, tempfile
from pathlib import Path

sys.dont_write_bytecode = True
with tempfile.TemporaryDirectory(prefix='cv-web-preflight-') as scratch:
    root = Path(scratch).resolve()
    backend = root / 'Backend'
    shutil.copytree(sys.argv[1], backend, ignore=shutil.ignore_patterns('wheels', '__pycache__'))
    data = root / 'data'
    docs = root / 'docs'
    data.mkdir()
    docs.mkdir()
    (data / 'config.json').write_text(json.dumps({
        'host': '127.0.0.1', 'scheduler_enabled': False, 'access_analytics_enabled': False,
    }), encoding='utf-8')
    os.environ['COLORVISION_WEB_DATA_DIR'] = str(data)
    os.environ['COLORVISION_WEB_DOCS_DIR'] = str(docs)
    os.environ['COLORVISION_WEB_RELEASE_ID'] = sys.argv[3]
    os.environ.pop('COLORVISION_ACTIVATION_HTTP_PRIVATE_KEY_FILE', None)
    sys.path[:0] = [str(backend), sys.argv[2]]
    previous_directory = Path.cwd()
    try:
        os.chdir(backend)
        import app as candidate
        try:
            if candidate.DB_PATH.resolve() != data / 'marketplace.db' or candidate.STORAGE.resolve() != data / 'storage':
                raise RuntimeError('Candidate backend did not use the isolated data directory')
            with candidate.app.test_client() as client:
                health = client.get('/api/health')
                ready = client.get('/api/ready')
                if (health.status_code != 200 or health.json.get('status') != 'ok'
                        or health.headers.get('X-ColorVision-Web-Release-Id') != sys.argv[3]
                        or health.headers.get('X-ColorVision-Web-Process-Id') != str(os.getpid())
                        or ready.status_code != 200 or ready.json.get('ready') is not True):
                    raise RuntimeError('Candidate backend did not pass isolated health and readiness')
        finally:
            candidate._helpers['access_recorder'].close()
    finally:
        os.chdir(previous_directory)
'@
    $null = Invoke-PythonChecked @('-I', '-S', '-c', $code, $source, $dependencyPath, $Manifest.Id) -TimeoutSeconds 60
}

function Read-ActiveRelease {
    if (-not (Test-Path -LiteralPath $activePath)) { return $null }
    $null = Assert-PlainItem $activePath
    $record = [IO.File]::ReadAllText($activePath, $utf8) | ConvertFrom-Json
    if ($record.schemaVersion -ne 1) { throw 'Invalid active release pointer.' }
    Assert-ReleaseId $record.releaseId
    if ($record.previousReleaseId) { Assert-ReleaseId $record.previousReleaseId }
    return $record
}

function Set-ActiveRelease {
    param($Record)
    if ($null -eq $Record) {
        if (Test-Path -LiteralPath $activePath) {
            $null = Assert-PlainItem $activePath
            [IO.File]::Delete($activePath)
        }
        return
    }
    $temporary = Join-Path $Root ('.active-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText($temporary, ($Record | ConvertTo-Json -Compress -Depth 4), $utf8)
        if (Test-Path -LiteralPath $activePath) {
            $null = Assert-PlainItem $activePath
            [IO.File]::Replace($temporary, $activePath, [System.Management.Automation.Language.NullString]::Value)
        } else {
            [IO.File]::Move($temporary, $activePath)
        }
    } finally {
        [IO.File]::Delete($temporary)
    }
}

function Get-ListenerId {
    $owners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique)
    if ($owners.Count -gt 1) { throw "Port $Port has multiple listening processes." }
    if ($owners.Count -eq 1) { return [int]$owners[0] }
    return 0
}

function Assert-ReleaseProcess {
    param([int]$ProcessId, [string]$ReleaseId)
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId"
    $appPath = Join-Path (Join-Path $releases $ReleaseId) 'Backend\app.py'
    $appPattern = '(?:^|\s)(?:"' + [Regex]::Escape($appPath) + '"|' + [Regex]::Escape($appPath) + ')(?:\s|$)'
    if (-not $process -or $process.ExecutablePath -ne $PythonExe -or
        $process.CommandLine -notmatch $appPattern -or $process.CommandLine -notmatch "(?:^|\s)--port\s+$Port(?:\s|$)") {
        throw "Port $Port is not owned by Web release $ReleaseId."
    }
}

function Read-ReleaseStatus {
    param([string]$ReleaseId, [int]$ProcessId, [switch]$RequireReady)
    $health = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/health" -UseBasicParsing -TimeoutSec 3
    $healthBody = $health.Content | ConvertFrom-Json
    if ([string]$health.Headers['X-ColorVision-Web-Release-Id'] -cne $ReleaseId -or
        [string]$health.Headers['X-ColorVision-Web-Process-Id'] -ne [string]$ProcessId -or $healthBody.status -ne 'ok') {
        throw 'Health response did not identify the expected release and listener.'
    }
    if ($RequireReady) {
        $ready = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/ready" -TimeoutSec 3
        if ($ready.ready -ne $true -or (Get-ListenerId) -ne $ProcessId) { throw 'Web release is not ready.' }
    }
}

function Stop-ReleaseTask {
    param([string]$ReleaseId)
    $owner = Get-ListenerId
    if ($owner) {
        if (-not $ReleaseId) { throw "Refusing to stop an unmanaged listener on port $Port." }
        Assert-ReleaseProcess $owner $ReleaseId
    }
    $task = Get-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
    if ($task.State -eq 'Running') { Stop-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath }
    $deadline = (Get-Date).AddSeconds(6)
    while (($owner = Get-ListenerId) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if ($owner) {
        if (-not $ReleaseId) { throw "Refusing to stop an unmanaged listener on port $Port." }
        Assert-ReleaseProcess $owner $ReleaseId
        Stop-Process -Id $owner -Force
        $deadline = (Get-Date).AddSeconds(5)
        while ((Get-ListenerId) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
        if (Get-ListenerId) { throw "Port $Port did not stop." }
    }
}

function Start-ReleaseTask {
    param([string]$ReleaseId)
    if (Get-ListenerId) { throw "Port $Port is already listening." }
    $started = Get-Date
    Start-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
    $deadline = $started.AddSeconds(60)
    $nextTaskCheck = $started.AddSeconds(4)
    $lastError = 'No listener appeared.'
    do {
        $owner = Get-ListenerId
        if ($owner) {
            Assert-ReleaseProcess $owner $ReleaseId
            try { Read-ReleaseStatus $ReleaseId $owner -RequireReady; return $owner }
            catch { $lastError = $_.Exception.Message }
        } elseif ((Get-Date) -ge $nextTaskCheck) {
            $task = Get-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
            $info = Get-ScheduledTaskInfo -TaskName $TaskName -TaskPath $TaskPath
            if ($task.State -eq 'Ready' -and $info.LastRunTime -ge $started.AddSeconds(-1)) {
                throw "Web release $ReleaseId exited before listening on port $Port (task result $($info.LastTaskResult))."
            }
            $nextTaskCheck = (Get-Date).AddSeconds(2)
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw "Web release $ReleaseId did not become ready: $lastError"
}

function Get-DatabaseSchema {
    param([string]$BackupPath)
    $database = Join-Path $DataDir 'marketplace.db'
    $null = Assert-PlainItem $database
    $code = @'
import sqlite3, sys
from pathlib import Path
from contextlib import closing
with closing(sqlite3.connect(Path(sys.argv[1]).as_uri() + '?mode=ro', uri=True, timeout=15)) as source:
    if len(sys.argv) > 2:
        with closing(sqlite3.connect(sys.argv[2], timeout=15)) as target:
            source.backup(target)
            if target.execute('PRAGMA quick_check').fetchone() != ('ok',):
                raise RuntimeError('SQLite backup is not valid')
            probe = target
            table = probe.execute("SELECT 1 FROM sqlite_master WHERE name='schema_version'").fetchone()
            row = probe.execute("SELECT value FROM schema_version WHERE key='version'").fetchone() if table else None
    else:
        table = source.execute("SELECT 1 FROM sqlite_master WHERE name='schema_version'").fetchone()
        row = source.execute("SELECT value FROM schema_version WHERE key='version'").fetchone() if table else None
    print(row[0] if row else 'unknown')
'@
    $arguments = @('-c', $code, $database)
    if ($BackupPath) { $arguments += $BackupPath }
    return Invoke-PythonChecked $arguments -TimeoutSeconds 120
}

function Assert-CompatibleSchema {
    param($Manifest, [string]$CurrentSchema)
    if ($CurrentSchema -eq 'unknown') { return }
    $schemaFile = Join-Path (Join-Path $releases $Manifest.Id) 'Backend\db\schema_version.py'
    $null = Assert-PlainItem $schemaFile
    $match = [Regex]::Match([IO.File]::ReadAllText($schemaFile, $utf8), '(?m)^CURRENT_SCHEMA_VERSION\s*=\s*(\d+)\s*$')
    if (-not $match.Success -or [int]$match.Groups[1].Value -lt [int]$CurrentSchema) {
        throw "Release $($Manifest.Id) cannot start on database schema $CurrentSchema."
    }
}

$total = [Diagnostics.Stopwatch]::StartNew()
$phase = [Diagnostics.Stopwatch]::StartNew()
$lock = $null
$existing = $null
$candidate = $null
$switchStarted = $false
$schemaBefore = $null
$result = [ordered]@{
    timestamp = [DateTimeOffset]::UtcNow.ToString('o')
    status = 'failed'
    module = 'Web'
    server = $env:COMPUTERNAME
    source = 'zip_package'
    operation = if ($Rollback) { 'rollback' } elseif ($StageOnly) { 'stage' } else { 'install' }
    previous_commit = $null
    deployed_commit = $null
    release_id = $null
    old_pid = $null
    new_pid = $null
    health = $null
    ready = $false
    backup_path = $null
    recovery = @()
    timings_ms = [ordered]@{}
}
try {
    Import-Module (Join-Path $PSScriptRoot 'NativeProcess.psm1') -Force
    Add-Type -AssemblyName System.IO.Compression
    $Root = [IO.Path]::GetFullPath($Root)
    $null = [IO.Directory]::CreateDirectory($Root)
    $Root = (Assert-PlainItem $Root -Directory).FullName
    $PythonExe = (Assert-PlainItem $PythonExe).FullName
    $DataDir = if ($DataDir) { [IO.Path]::GetFullPath($DataDir) } else { Join-Path $Root 'data' }
    $activePath = Join-Path $Root 'active.json'
    $lockPath = Join-Path $Root '.deploy.lock'
    if (Test-Path -LiteralPath $lockPath) { $null = Assert-PlainItem $lockPath }
    $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $releases = Join-Path $Root 'releases'
    $dependencies = Join-Path $Root 'dependencies'
    foreach ($directory in @($releases, $dependencies)) {
        $null = [IO.Directory]::CreateDirectory($directory)
        $null = Assert-PlainItem $directory -Directory
    }

    $existing = Read-ActiveRelease
    $previous = $null
    if ($existing) {
        $previous = Read-Release (Join-Path $releases $existing.releaseId)
        if ($existing.backendRequirementsSha256 -ne $previous.RequirementsHash) { throw 'Active release requirements digest differs from the manifest.' }
        $result.previous_commit = $previous.Commit
    }
    if ($Rollback) {
        if (-not $existing -or -not $existing.previousReleaseId) { throw 'No previous Web release is recorded.' }
        $previousDirectory = Join-Path $releases $existing.previousReleaseId
        $candidate = Read-Release $previousDirectory
        Assert-InstalledFiles $previousDirectory $candidate
    } else {
        $candidate = Install-ReleaseFiles ([IO.Path]::GetFullPath($ZipPath)) $Sha256 $releases
    }
    $result.release_id = $candidate.Id
    $result.deployed_commit = $candidate.Commit
    $result.timings_ms.package = $phase.ElapsedMilliseconds
    $phase.Restart()
    Prepare-Dependencies $candidate
    $result.timings_ms.dependencies = $phase.ElapsedMilliseconds
    $phase.Restart()
    if (-not $Rollback) {
        try { Test-CandidateBackend $candidate }
        finally { $result.timings_ms.preflight = $phase.ElapsedMilliseconds }
        $phase.Restart()
    }

    if ($StageOnly) {
        $result.status = 'staged'
    } else {
        $null = Assert-PlainItem $DataDir -Directory
        $null = Assert-PlainItem (Join-Path $DataDir 'config.json')
        $null = Get-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
        $oldListener = Get-ListenerId
        if ($oldListener) {
            if (-not $existing) { throw "Refusing to stop an unmanaged listener on port $Port." }
            Assert-ReleaseProcess $oldListener $existing.releaseId
            $result.old_pid = $oldListener
        }
        $alreadyHealthy = $false
        if ($oldListener -and $existing.releaseId -ceq $candidate.Id -and -not $Rollback) {
            try {
                Read-ReleaseStatus $candidate.Id $oldListener -RequireReady
                $alreadyHealthy = $true
            } catch {
                Write-Verbose 'The current release did not pass readiness. Restarting it.'
            }
        }
        if ($alreadyHealthy) {
            $result.new_pid = $oldListener
            $result.health = 'ok'
            $result.ready = $true
            $result.status = 'already_current'
        } else {
            $backups = Join-Path $Root 'backups'
            $null = [IO.Directory]::CreateDirectory($backups)
            $null = Assert-PlainItem $backups -Directory
            $backup = Join-Path $backups ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N') + '.db')
            $schemaBefore = Get-DatabaseSchema $backup
            $result.backup_path = $backup
            Assert-CompatibleSchema $candidate $schemaBefore
            $result.timings_ms.backup = $phase.ElapsedMilliseconds
            $phase.Restart()

            $switchStarted = $true
            Stop-ReleaseTask $(if ($existing) { $existing.releaseId })
            $pointer = [ordered]@{
                schemaVersion = 1
                releaseId = $candidate.Id
                previousReleaseId = if ($existing -and $existing.releaseId -ne $candidate.Id) { $existing.releaseId } elseif ($existing) { $existing.previousReleaseId } else { $null }
                backendRequirementsSha256 = $candidate.RequirementsHash
            }
            Set-ActiveRelease $pointer
            $result.new_pid = Start-ReleaseTask $candidate.Id
            $result.timings_ms.switch = $phase.ElapsedMilliseconds
            $result.health = 'ok'
            $result.ready = $true
            $result.status = 'success'
        }
    }
} catch {
    $result.error = $_.Exception.Message
    if ($switchStarted) {
        $result.timings_ms.switch = $phase.ElapsedMilliseconds
        $phase.Restart()
        try {
            $active = Read-ActiveRelease
            Stop-ReleaseTask $(if ($active) { $active.releaseId })
            if ($existing) {
                $schemaAfter = Get-DatabaseSchema
                if ($schemaBefore -eq 'unknown' -or $schemaAfter -ne $schemaBefore) {
                    throw "Automatic rollback blocked after database schema changed ($schemaBefore -> $schemaAfter)."
                }
                Assert-CompatibleSchema $previous $schemaAfter
                Set-ActiveRelease $existing
                $result.recovered_pid = Start-ReleaseTask $existing.releaseId
                $result.recovery = @('previous_release_verified')
            } else {
                Set-ActiveRelease $null
                $result.recovery = @('no_previous_package_release')
            }
        } catch {
            $result.recovery = @('recovery_failed')
            $result.recovery_error = $_.Exception.Message
        } finally {
            $result.timings_ms.recovery = $phase.ElapsedMilliseconds
        }
    }
} finally {
    $result.timings_ms.total = $total.ElapsedMilliseconds
    if ($lock) {
        try {
            if ($HistoryPath -and -not $StageOnly) {
                Import-Module (Join-Path $PSScriptRoot 'DeploymentHistory.psm1') -Force
                $null = Write-WebDeploymentHistory -HistoryPath $HistoryPath -Record $result
            }
        } catch {
            $result.history_error = $_.Exception.Message
        } finally { $lock.Dispose() }
    }
}
$result | ConvertTo-Json -Depth 8 -Compress
if ($result.status -eq 'failed') { exit 1 }
