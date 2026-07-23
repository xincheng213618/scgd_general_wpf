[CmdletBinding()]
param(
    [string]$SshTarget = "cv-publish",
    [string]$RepoPath = "C:\Users\Administrator\Desktop\scgd_general_wpf",
    [string]$StoragePath = "D:\ColorVision",
    [string]$Branch = "develop",
    [string]$TaskPath = "\ColorVision\",
    [string]$TaskName = "ColorVisionWeb",
    [int]$Port = 9998,
    [switch]$Force,
    [switch]$SkipTests,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-PowerShellLiteral {
    param([Parameter(Mandatory)][string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-RemotePowerShell {
    param(
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$ScriptText
    )

    $payload = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($ScriptText))
    $loader = '$global:ProgressPreference = ''SilentlyContinue''; $payload = [Console]::In.ReadToEnd(); $scriptText = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($payload.Trim())); $scriptBlock = [ScriptBlock]::Create($scriptText); & $scriptBlock'
    $encodedLoader = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($loader))
    $sshArguments = @(
        $Target,
        'powershell',
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy',
        'Bypass',
        '-EncodedCommand',
        $encodedLoader
    )
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $remoteOutput = @($payload | & ssh @sshArguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    foreach ($outputItem in $remoteOutput) {
        $outputText = [string]$outputItem
        if ($outputText -eq '#< CLIXML' -or $outputText -eq 'System.Management.Automation.RemoteException' -or $outputText.StartsWith('<Objs Version=')) {
            continue
        }
        Write-Output $outputText
    }
    if ($exitCode -ne 0) {
        throw "NAS deployment failed with SSH exit code $exitCode."
    }
}

if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
    throw "OpenSSH client 'ssh' was not found."
}

$remoteTemplate = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoPath = __REPO_PATH__
$storagePath = __STORAGE_PATH__
$branch = __BRANCH__
$taskPath = __TASK_PATH__
$taskName = __TASK_NAME__
$port = __PORT__
$forceDeploy = __FORCE__
$skipTests = __SKIP_TESTS__
$dryRun = __DRY_RUN__

$frontendPath = Join-Path $repoPath 'Web\Frontend'
$backendPath = Join-Path $repoPath 'Web\Backend'
$configPath = Join-Path $backendPath 'config.json'
$databasePath = Join-Path $backendPath 'marketplace.db'
$liveDistPath = Join-Path $frontendPath 'dist'
$historyPath = Join-Path $storagePath 'web-deploy-history.jsonl'
$backupRoot = Join-Path $storagePath 'web-deploy-backups'
$pythonExe = 'C:\Users\Administrator\AppData\Local\Programs\Python\Python310\python.exe'
$nodeExe = 'C:\Program Files\nodejs\node.exe'
$npmExe = 'C:\Program Files\nodejs\npm.cmd'
$gitExe = 'C:\Program Files\Git\cmd\git.exe'
$deploymentStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = $null
$rollbackDistPath = Join-Path $frontendPath "dist.rollback-$deploymentStamp"
$stagedDistPath = Join-Path $frontendPath "dist.deploy-$deploymentStamp"
$previousCommit = $null
$targetCommit = $null
$deployedCommit = $null
$oldPid = $null
$newPid = $null
$distSwapped = $false

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList,
        [string]$WorkingDirectory
    )

    if ($WorkingDirectory) {
        Push-Location -LiteralPath $WorkingDirectory
    }
    try {
        & $FilePath @ArgumentList
        $exitCode = $LASTEXITCODE
    } finally {
        if ($WorkingDirectory) {
            Pop-Location
        }
    }
    if ($exitCode -ne 0) {
        throw "$FilePath failed with exit code $exitCode."
    }
}

function Get-NativeText {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList
    )

    $output = @(& $FilePath @ArgumentList)
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
    return (($output -join "`n").Trim())
}

function Write-DeploymentHistory {
    param([Parameter(Mandatory)]$Record)

    if (-not (Test-Path -LiteralPath $storagePath)) {
        throw "Storage path does not exist: $storagePath"
    }
    Add-Content -LiteralPath $historyPath -Value ($Record | ConvertTo-Json -Compress -Depth 6) -Encoding UTF8
}

function Get-WebListener {
    return Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Assert-WebProcess {
    param([Parameter(Mandatory)]$Listener)

    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $($Listener.OwningProcess)"
    if (-not $process -or $process.Name -ne 'python.exe' -or $process.ExecutablePath -ne $pythonExe -or $process.CommandLine -notmatch "app\.py\s+--port\s+$port") {
        throw "Port $port is not owned by the expected ColorVision Web process."
    }
    return $process
}

function Stop-WebService {
    $listener = Get-WebListener
    if ($listener) {
        Assert-WebProcess -Listener $listener | Out-Null
    }

    $task = Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName
    if ($task.State -eq 'Running') {
        Stop-ScheduledTask -TaskPath $taskPath -TaskName $taskName
    }

    $deadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 500
        $listener = Get-WebListener
    } while ($listener -and (Get-Date) -lt $deadline)

    if ($listener) {
        Assert-WebProcess -Listener $listener | Out-Null
        Stop-Process -Id $listener.OwningProcess -Force
    }

    $deadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 500
        $listener = Get-WebListener
    } while ($listener -and (Get-Date) -lt $deadline)

    if ($listener) {
        throw "Port $port did not stop."
    }
}

function Start-WebService {
    Start-ScheduledTask -TaskPath $taskPath -TaskName $taskName
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Seconds 1
        $listener = Get-WebListener
    } while (-not $listener -and (Get-Date) -lt $deadline)

    if (-not $listener) {
        $taskInfo = Get-ScheduledTaskInfo -TaskPath $taskPath -TaskName $taskName
        throw "Scheduled task did not restore port $port; result=$($taskInfo.LastTaskResult)."
    }

    Assert-WebProcess -Listener $listener | Out-Null
}

function Wait-WebReady {
    $baseUrl = "http://127.0.0.1:$port"
    $deadline = (Get-Date).AddSeconds(90)
    $lastError = $null
    do {
        try {
            $health = Invoke-RestMethod -Uri "$baseUrl/api/health" -TimeoutSec 10
            $ready = Invoke-RestMethod -Uri "$baseUrl/api/ready" -TimeoutSec 10
            if ($health.status -eq 'ok' -and $ready.ready -eq $true) {
                return [pscustomobject]@{ health = $health; ready = $ready }
            }
            $lastError = "health=$($health.status), ready=$($ready.ready)"
        } catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "Web readiness check timed out: $lastError"
}

function Remove-SafeDeployDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ExpectedParent,
        [Parameter(Mandatory)][string[]]$AllowedLeafNames
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    $resolvedPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $resolvedParent = [IO.Path]::GetFullPath($ExpectedParent).TrimEnd('\')
    $actualParent = [IO.Path]::GetDirectoryName($resolvedPath).TrimEnd('\')
    $leafName = [IO.Path]::GetFileName($resolvedPath)
    if (-not $actualParent.Equals($resolvedParent, [StringComparison]::OrdinalIgnoreCase) -or $AllowedLeafNames -notcontains $leafName) {
        throw "Refusing to remove unexpected deployment path: $resolvedPath"
    }
    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Get-ResponseSize {
    param([Parameter(Mandatory)][string]$Uri)

    $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 30
    return [Text.Encoding]::UTF8.GetByteCount([string]$response.Content)
}

try {
    Write-Output "ColorVision Web NAS deployment"
    Write-Output "Repository: $repoPath"
    Write-Output "Branch: $branch"
    Write-Output "Port: $port"

    foreach ($requiredPath in @($repoPath, $storagePath, $frontendPath, $backendPath, $configPath, $databasePath, $pythonExe, $nodeExe, $npmExe, $gitExe)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Required deployment path does not exist: $requiredPath"
        }
    }
    Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName | Out-Null

    $currentBranch = Get-NativeText -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'branch', '--show-current')
    if ($currentBranch -ne $branch) {
        throw "NAS repository is on branch '$currentBranch', expected '$branch'."
    }

    $previousCommit = Get-NativeText -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'rev-parse', 'HEAD')
    $trackedChanges = @()
    $trackedChanges += @(& $gitExe -C $repoPath diff --name-only)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect unstaged changes.' }
    $trackedChanges += @(& $gitExe -C $repoPath diff --cached --name-only)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect staged changes.' }
    $unexpectedChanges = @($trackedChanges | Sort-Object -Unique | Where-Object { $_ -and $_ -ne 'Web/Backend/config.json' })
    if ($unexpectedChanges.Count -gt 0) {
        throw "NAS has unexpected tracked changes. Commit or remove them before deploying:`n$($unexpectedChanges -join "`n")"
    }

    if ($dryRun) {
        $remoteLine = Get-NativeText -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'ls-remote', 'origin', "refs/heads/$branch")
        $targetCommit = ($remoteLine -split '\s+')[0]
        $listener = Get-WebListener
        $summary = [ordered]@{
            status = 'dry_run'
            current_commit = $previousCommit
            target_commit = $targetCommit
            update_required = ($previousCommit -ne $targetCommit)
            force = [bool]$forceDeploy
            listener_pid = if ($listener) { [int]$listener.OwningProcess } else { $null }
            task_state = (Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName).State.ToString()
            tracked_changes = @($trackedChanges | Sort-Object -Unique)
        }
        Write-Output ($summary | ConvertTo-Json -Depth 4)
        exit 0
    }

    Invoke-NativeCommand -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'fetch', 'origin')
    $targetCommit = Get-NativeText -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'rev-parse', "origin/$branch")

    if ($previousCommit -eq $targetCommit -and -not $forceDeploy) {
        try {
            $readiness = Wait-WebReady
            $record = [ordered]@{
                timestamp = (Get-Date).ToString('o')
                status = 'already_current'
                module = 'Web'
                server = $env:COMPUTERNAME
                commit = $previousCommit
                health = $readiness.health.status
                ready = [bool]$readiness.ready.ready
            }
            Write-DeploymentHistory -Record $record
            Write-Output "NAS Web is already current and healthy: $previousCommit"
            exit 0
        } catch {
            Write-Warning "Current deployment is unhealthy; forcing rebuild and restart: $($_.Exception.Message)"
        }
    }

    $backupPath = Join-Path $backupRoot $deploymentStamp
    $resolvedBackupRoot = [IO.Path]::GetFullPath($backupRoot).TrimEnd('\')
    $resolvedBackupPath = [IO.Path]::GetFullPath($backupPath).TrimEnd('\')
    if (-not $resolvedBackupPath.StartsWith($resolvedBackupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Backup path escaped intended root: $resolvedBackupPath"
    }
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $backupPath 'config.json')
    $lockPath = Join-Path $frontendPath 'package-lock.json'
    if (Test-Path -LiteralPath $lockPath) {
        Copy-Item -LiteralPath $lockPath -Destination (Join-Path $backupPath 'package-lock.json.before')
    }
    if (Test-Path -LiteralPath $liveDistPath) {
        Copy-Item -LiteralPath $liveDistPath -Destination (Join-Path $backupPath 'dist-old') -Recurse
    }

    $databaseBackupPath = Join-Path $backupPath 'marketplace.db'
    $sqliteBackupCode = "import sqlite3,sys; source=sqlite3.connect(sys.argv[1]); target=sqlite3.connect(sys.argv[2]); source.backup(target); target.close(); source.close()"
    Invoke-NativeCommand -FilePath $pythonExe -ArgumentList @('-c', $sqliteBackupCode, $databasePath, $databaseBackupPath)

    $beforeRecord = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        status = 'backup_created'
        previous_commit = $previousCommit
        target_commit = $targetCommit
        backup_path = $backupPath
    }
    $beforeRecord | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupPath 'deployment-before.json') -Encoding UTF8

    if ($previousCommit -ne $targetCommit) {
        $pullSucceeded = $false
        try {
            Invoke-NativeCommand -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'restore', '--', 'Web/Backend/config.json')
            Invoke-NativeCommand -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'pull', '--ff-only', 'origin', $branch)
            $pullSucceeded = $true
        } finally {
            Copy-Item -LiteralPath (Join-Path $backupPath 'config.json') -Destination $configPath -Force
        }
        if (-not $pullSucceeded) {
            throw 'Git synchronization did not complete.'
        }
    }

    $deployedCommit = Get-NativeText -FilePath $gitExe -ArgumentList @('-C', $repoPath, 'rev-parse', 'HEAD')
    if ($deployedCommit -ne $targetCommit) {
        throw "Deployed commit $deployedCommit does not match target $targetCommit."
    }

    $requirementsChanged = @(& $gitExe -C $repoPath diff --name-only "$previousCommit..$deployedCommit" -- Web/Backend/requirements.txt)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect backend dependency changes.' }
    if ($requirementsChanged.Count -gt 0) {
        Invoke-NativeCommand -FilePath $pythonExe -ArgumentList @('-m', 'pip', 'install', '-r', 'requirements.txt') -WorkingDirectory $backendPath
    }

    Invoke-NativeCommand -FilePath $npmExe -ArgumentList @('ci', '--no-audit', '--no-fund') -WorkingDirectory $frontendPath
    $tscExe = Join-Path $frontendPath 'node_modules\.bin\tsc.cmd'
    $viteExe = Join-Path $frontendPath 'node_modules\.bin\vite.cmd'
    foreach ($buildTool in @($tscExe, $viteExe)) {
        if (-not (Test-Path -LiteralPath $buildTool)) {
            throw "Frontend build tool is missing: $buildTool"
        }
    }
    Invoke-NativeCommand -FilePath $tscExe -ArgumentList @('-b') -WorkingDirectory $frontendPath
    Invoke-NativeCommand -FilePath $viteExe -ArgumentList @('build', '--outDir', $stagedDistPath, '--emptyOutDir', '--logLevel', 'warn') -WorkingDirectory $frontendPath
    if (-not (Test-Path -LiteralPath (Join-Path $stagedDistPath 'index.html'))) {
        throw 'Staged frontend index.html was not generated.'
    }

    if (-not $skipTests) {
        Invoke-NativeCommand -FilePath $pythonExe -ArgumentList @('-m', 'unittest', 'test_access_analytics', 'test_frontend_spa', 'test_page_contexts') -WorkingDirectory $backendPath
    }

    $listenerBeforeRestart = Get-WebListener
    if ($listenerBeforeRestart) {
        Assert-WebProcess -Listener $listenerBeforeRestart | Out-Null
        $oldPid = [int]$listenerBeforeRestart.OwningProcess
    }
    Stop-WebService
    if (Test-Path -LiteralPath $rollbackDistPath) {
        throw "Rollback staging path already exists: $rollbackDistPath"
    }
    if (Test-Path -LiteralPath $liveDistPath) {
        Move-Item -LiteralPath $liveDistPath -Destination $rollbackDistPath
    }
    Move-Item -LiteralPath $stagedDistPath -Destination $liveDistPath
    $distSwapped = $true

    Start-WebService
    $listenerAfterRestart = Get-WebListener
    if (-not $listenerAfterRestart) {
        throw "Port $port disappeared after the service restart."
    }
    $newPid = [int]$listenerAfterRestart.OwningProcess
    $readiness = Wait-WebReady
    $baseUrl = "http://127.0.0.1:$port"
    $compactHomeBytes = Get-ResponseSize -Uri "$baseUrl/api/site/home?view=compact"
    $compactReleaseBytes = Get-ResponseSize -Uri "$baseUrl/api/site/releases?view=compact&page=1&page_size=20"
    $compactChangelogBytes = Get-ResponseSize -Uri "$baseUrl/api/site/changelog?view=compact&page=1&page_size=20"
    $trafficAssets = @(Get-ChildItem -LiteralPath (Join-Path $liveDistPath 'assets') -Filter 'TrafficPage-*.js' -File)
    if ($trafficAssets.Count -eq 0) {
        throw 'TrafficPage frontend asset is missing from the live build.'
    }

    $analyticsCode = "import json,sqlite3,sys; db=sqlite3.connect(sys.argv[1]); names=['access_daily','access_route_daily','access_client_daily','access_visitor_daily']; print(json.dumps({name:db.execute('select count(*) from '+name).fetchone()[0] for name in names})); db.close()"
    $analyticsJson = @(& $pythonExe -c $analyticsCode $databasePath)
    if ($LASTEXITCODE -ne 0) { throw 'Access analytics verification failed.' }
    $analytics = (($analyticsJson -join '') | ConvertFrom-Json)

    Remove-SafeDeployDirectory -Path $rollbackDistPath -ExpectedParent $frontendPath -AllowedLeafNames @([IO.Path]::GetFileName($rollbackDistPath))

    $successRecord = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        status = 'success'
        module = 'Web'
        server = $env:COMPUTERNAME
        previous_commit = $previousCommit
        deployed_commit = $deployedCommit
        backup_path = $backupPath
        frontend_build = 'success'
        backend_targeted_tests = if ($skipTests) { 'skipped' } else { 25 }
        old_pid = $oldPid
        new_pid = $newPid
        health = $readiness.health.status
        ready = [bool]$readiness.ready.ready
        compact_home_bytes = $compactHomeBytes
        compact_releases_bytes = $compactReleaseBytes
        compact_changelog_bytes = $compactChangelogBytes
        analytics = $analytics
    }
    Write-DeploymentHistory -Record $successRecord
    $successRecord | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupPath 'deployment-after.json') -Encoding UTF8
    Write-Output ($successRecord | ConvertTo-Json -Depth 6)
} catch {
    $failureMessage = $_.Exception.Message
    $recovery = @()

    try {
        if (-not $distSwapped -and (Test-Path -LiteralPath $stagedDistPath)) {
            Remove-SafeDeployDirectory -Path $stagedDistPath -ExpectedParent $frontendPath -AllowedLeafNames @([IO.Path]::GetFileName($stagedDistPath))
            $recovery += 'removed_staged_frontend'
        }
        if (-not (Get-WebListener)) {
            if ($distSwapped -and (Test-Path -LiteralPath $rollbackDistPath)) {
                Remove-SafeDeployDirectory -Path $liveDistPath -ExpectedParent $frontendPath -AllowedLeafNames @('dist')
                Move-Item -LiteralPath $rollbackDistPath -Destination $liveDistPath
                $recovery += 'restored_previous_frontend'
            }
            Start-WebService
            $recovery += 'service_restart_attempted'
        }
    } catch {
        $recovery += "recovery_failed: $($_.Exception.Message)"
    }

    $failureRecord = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        status = 'failed'
        module = 'Web'
        server = $env:COMPUTERNAME
        previous_commit = $previousCommit
        target_commit = $targetCommit
        deployed_commit = $deployedCommit
        backup_path = $backupPath
        error = $failureMessage
        recovery = $recovery
    }
    try {
        Write-DeploymentHistory -Record $failureRecord
        if ($backupPath -and (Test-Path -LiteralPath $backupPath)) {
            $failureRecord | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupPath 'deployment-failed.json') -Encoding UTF8
        }
    } catch {
        Write-Warning "Could not write deployment failure record: $($_.Exception.Message)"
    }
    Write-Output "DEPLOYMENT_ERROR=$failureMessage"
    exit 1
}
'@

$replacements = [ordered]@{
    '__REPO_PATH__' = ConvertTo-PowerShellLiteral $RepoPath
    '__STORAGE_PATH__' = ConvertTo-PowerShellLiteral $StoragePath
    '__BRANCH__' = ConvertTo-PowerShellLiteral $Branch
    '__TASK_PATH__' = ConvertTo-PowerShellLiteral $TaskPath
    '__TASK_NAME__' = ConvertTo-PowerShellLiteral $TaskName
    '__PORT__' = $Port.ToString([Globalization.CultureInfo]::InvariantCulture)
    '__FORCE__' = if ($Force) { '$true' } else { '$false' }
    '__SKIP_TESTS__' = if ($SkipTests) { '$true' } else { '$false' }
    '__DRY_RUN__' = if ($DryRun) { '$true' } else { '$false' }
}

$remoteScript = $remoteTemplate
foreach ($replacement in $replacements.GetEnumerator()) {
    $remoteScript = $remoteScript.Replace($replacement.Key, [string]$replacement.Value)
}

Write-Host "Connecting to $SshTarget..."
Invoke-RemotePowerShell -Target $SshTarget -ScriptText $remoteScript
