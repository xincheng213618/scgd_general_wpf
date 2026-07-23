using ColorVision.UI.ServiceHost;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ServiceHost
{
    public enum ServiceHostInstallState
    {
        NotInstalled,
        Stopped,
        Running,
        Unknown,
    }

    public sealed class ServiceHostStatus
    {
        private static readonly Version MinimumSelfUpdateVersion = new(1, 4, 10, 5);

        public ServiceHostInstallState State { get; init; }

        public string RawOutput { get; init; } = string.Empty;

        public string PackageExecutablePath { get; init; } = string.Empty;

        public string InstalledExecutablePath { get; init; } = string.Empty;

        public Version? PackageVersion { get; init; }

        public Version? InstalledVersion { get; init; }

        public Version? RunningVersion { get; init; }

        public string RunningProcessPath { get; init; } = string.Empty;

        public bool IsPackageAvailable => File.Exists(PackageExecutablePath);

        public bool NeedsInstall => State == ServiceHostInstallState.NotInstalled;

        public bool NeedsUpdate => IsPackageAvailable
            && PackageVersion != null
            && (InstalledVersion == null || PackageVersion > InstalledVersion || (RunningVersion != null && PackageVersion > RunningVersion));

        public bool NeedsRepair => IsPackageAvailable
            && (State == ServiceHostInstallState.Stopped
                || (State == ServiceHostInstallState.Running && (RunningVersion == null || !HasExpectedRunningPath)));

        public bool HasExpectedRunningPath => IsSameExecutablePath(RunningProcessPath, InstalledExecutablePath);

        public bool IsReady => State == ServiceHostInstallState.Running
            && RunningVersion != null
            && HasExpectedRunningPath;

        public bool HasCurrentOrNewerInstalledVersion => PackageVersion != null
            && InstalledVersion != null
            && InstalledVersion >= PackageVersion;

        public bool WouldInstallDowngrade => PackageVersion != null
            && ((InstalledVersion != null && InstalledVersion > PackageVersion)
                || (RunningVersion != null && RunningVersion > PackageVersion));

        public bool CanSelfUpdate => State == ServiceHostInstallState.Running
            && NeedsUpdate
            && RunningVersion != null
            && HasExpectedRunningPath
            && RunningVersion.CompareTo(MinimumSelfUpdateVersion) >= 0;

        public string DisplayText => State switch
        {
            ServiceHostInstallState.NotInstalled => $"Not installed, package {FormatVersion(PackageVersion)}",
            ServiceHostInstallState.Stopped => $"Stopped, installed {FormatVersion(InstalledVersion)}, package {FormatVersion(PackageVersion)}",
            ServiceHostInstallState.Running => $"Running, installed {FormatVersion(InstalledVersion)}, running {FormatVersion(RunningVersion)}, package {FormatVersion(PackageVersion)}",
            _ => $"Unknown, installed {FormatVersion(InstalledVersion)}, package {FormatVersion(PackageVersion)}",
        };

        private static string FormatVersion(Version? version)
        {
            return version?.ToString() ?? "unknown";
        }

        private static bool IsSameExecutablePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            try
            {
                string normalizedLeft = Path.GetFullPath(left.Trim().Trim('"'));
                string normalizedRight = Path.GetFullPath(right.Trim().Trim('"'));
                return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public sealed class ServiceHostOperationResult
    {
        public bool Success { get; init; }

        public int ExitCode { get; init; }

        public string Output { get; init; } = string.Empty;

        public string Error { get; init; } = string.Empty;

        public string Summary
        {
            get
            {
                StringBuilder builder = new();
                builder.AppendLine($"Success: {Success}, ExitCode: {ExitCode}");
                if (!string.IsNullOrWhiteSpace(Output))
                    builder.AppendLine(Output.Trim());
                if (!string.IsNullOrWhiteSpace(Error))
                    builder.AppendLine(Error.Trim());
                return builder.ToString().Trim();
            }
        }
    }

    public sealed class ServiceHostEnsureResult
    {
        public bool Success { get; init; }

        public int ExitCode { get; init; }

        public ServiceHostStatus Status { get; init; } = new() { State = ServiceHostInstallState.Unknown };

        public string Error { get; init; } = string.Empty;

        public string Details { get; init; } = string.Empty;

        public string Summary => Success
            ? Details
            : string.IsNullOrWhiteSpace(Details) ? Error : $"{Error}{Environment.NewLine}{Details}";
    }

    public static class ColorVisionServiceHostManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ColorVisionServiceHostManager));
        private static readonly TimeSpan DefaultReadinessTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ReadinessPollInterval = TimeSpan.FromMilliseconds(500);

        public static bool IsAdministrator()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static Task<ServiceHostOperationResult> InstallAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(ServiceHostProtocol.PackageExecutablePath))
            {
                return Task.FromResult(new ServiceHostOperationResult
                {
                    Success = false,
                    ExitCode = -1,
                    Error = $"Service host executable was not found: {ServiceHostProtocol.PackageExecutablePath}",
                });
            }

            return RunPowerShellScriptAsync(CreateInstallScript(), requireAdministrator: true, cancellationToken);
        }

        public static Task<ServiceHostOperationResult> UninstallAsync(CancellationToken cancellationToken = default)
        {
            return RunPowerShellScriptAsync(CreateUninstallScript(), requireAdministrator: true, cancellationToken);
        }

        public static Task<ServiceHostOperationResult> StartAsync(CancellationToken cancellationToken = default)
        {
            return RunPowerShellScriptAsync(CreateStartScript(), requireAdministrator: true, cancellationToken);
        }

        public static Task<ServiceHostOperationResult> StopAsync(CancellationToken cancellationToken = default)
        {
            return RunPowerShellScriptAsync(CreateStopScript(), requireAdministrator: true, cancellationToken);
        }

        public static Task<ServiceHostEnsureResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            return EnsureReadyAsync(DefaultReadinessTimeout, cancellationToken);
        }

        internal static async Task<ServiceHostEnsureResult> EnsureReadyAsync(
            TimeSpan readinessTimeout,
            CancellationToken cancellationToken = default)
        {
            if (readinessTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(readinessTimeout), "The readiness timeout must be greater than zero.");

            List<string> attempts = [];
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Unknown,
                PackageExecutablePath = ServiceHostProtocol.PackageExecutablePath,
                InstalledExecutablePath = ServiceHostProtocol.InstalledExecutablePath,
            };
            try
            {
                status = await QueryStatusAsync(cancellationToken).ConfigureAwait(false);
                if (IsReadyForPackagedVersion(status))
                    return CreateEnsureSuccess(status, attempts);

                if (!status.IsPackageAvailable)
                {
                    return CreateEnsureFailure(
                        $"Service host executable was not found: {status.PackageExecutablePath}",
                        status,
                        attempts);
                }

                if (status.PackageVersion == null)
                {
                    return CreateEnsureFailure(
                        $"Unable to read the packaged service host version: {status.PackageExecutablePath}",
                        status,
                        attempts);
                }

                ServiceHostStartupAction action = ServiceHostStartupUpdateChecker.ResolveAction(status);
                if (action == ServiceHostStartupAction.None)
                {
                    return status.WouldInstallDowngrade
                        ? CreateEnsureFailure(
                            $"Refusing to replace a newer service host with package {status.PackageVersion}. Installed={status.InstalledVersion}, Running={status.RunningVersion}.",
                            status,
                            attempts)
                        : CreateEnsureFailure($"ColorVisionServiceHost is not ready: {status.DisplayText}", status, attempts);
                }

                if (action == ServiceHostStartupAction.Start)
                {
                    ServiceHostOperationResult startResult = await StartAsync(cancellationToken).ConfigureAwait(false);
                    AddAttempt(attempts, "Start", startResult);
                    if (startResult.Success)
                    {
                        status = await WaitForReadyAsync(readinessTimeout, cancellationToken).ConfigureAwait(false);
                        if (IsReadyForPackagedVersion(status))
                            return CreateEnsureSuccess(status, attempts);
                    }
                    else
                    {
                        status = await QueryStatusAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (IsReadyForPackagedVersion(status))
                        return CreateEnsureSuccess(status, attempts);

                    if (status.WouldInstallDowngrade)
                    {
                        return CreateEnsureFailure(
                            $"The installed service host could not be started, and repairing it from package {status.PackageVersion} would downgrade version {status.InstalledVersion ?? status.RunningVersion}.",
                            status,
                            attempts);
                    }

                    return await InstallAndWaitForReadyAsync(readinessTimeout, status, attempts, cancellationToken).ConfigureAwait(false);
                }

                if (action == ServiceHostStartupAction.SelfUpdate)
                {
                    ServiceHostOperationResult selfUpdateResult = await SelfUpdateAsync(cancellationToken).ConfigureAwait(false);
                    AddAttempt(attempts, "Self Update", selfUpdateResult);
                    if (selfUpdateResult.Success)
                    {
                        status = await WaitForReadyAsync(readinessTimeout, cancellationToken).ConfigureAwait(false);
                        if (IsReadyForPackagedVersion(status))
                            return CreateEnsureSuccess(status, attempts);
                    }
                    else
                    {
                        status = await QueryStatusAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (IsReadyForPackagedVersion(status))
                        return CreateEnsureSuccess(status, attempts);

                    if (status.WouldInstallDowngrade)
                    {
                        return CreateEnsureFailure(
                            $"Silent service host update failed, and elevated repair from package {status.PackageVersion} would downgrade the installed service.",
                            status,
                            attempts);
                    }

                    return await InstallAndWaitForReadyAsync(readinessTimeout, status, attempts, cancellationToken).ConfigureAwait(false);
                }

                if (status.WouldInstallDowngrade)
                {
                    return CreateEnsureFailure(
                        $"Refusing to repair ColorVisionServiceHost from older package {status.PackageVersion}. Installed={status.InstalledVersion}, Running={status.RunningVersion}.",
                        status,
                        attempts);
                }

                return await InstallAndWaitForReadyAsync(readinessTimeout, status, attempts, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error("Failed to ensure that ColorVisionServiceHost is ready.", ex);
                return CreateEnsureFailure(ex.Message, status, attempts);
            }
        }

        public static async Task<ServiceHostOperationResult> SelfUpdateAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(ServiceHostProtocol.PackageExecutablePath))
            {
                return new ServiceHostOperationResult
                {
                    Success = false,
                    ExitCode = -1,
                    Error = $"Service host executable was not found: {ServiceHostProtocol.PackageExecutablePath}",
                };
            }

            try
            {
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .SelfUpdateAsync(ServiceHostProtocol.PackageDirectory, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new ServiceHostOperationResult
                {
                    Success = response.Success,
                    ExitCode = response.Success ? 0 : 1,
                    Output = response.ToDisplayText(),
                    Error = response.Success ? string.Empty : response.Message,
                };
            }
            catch (Exception ex)
            {
                log.Error("Failed to request service host self update.", ex);
                return new ServiceHostOperationResult
                {
                    Success = false,
                    ExitCode = -1,
                    Error = ex.Message,
                };
            }
        }

        public static async Task<ServiceHostStatus> QueryStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Version? packageVersion = GetExecutableVersion(ServiceHostProtocol.PackageExecutablePath);
            Version? installedVersion = GetExecutableVersion(ServiceHostProtocol.InstalledExecutablePath);

            try
            {
                using ServiceController controller = new(ServiceHostProtocol.ServiceName);
                ServiceControllerStatus status = controller.Status;
                ServiceHostInstallState state = status switch
                {
                    ServiceControllerStatus.Running => ServiceHostInstallState.Running,
                    ServiceControllerStatus.Stopped => ServiceHostInstallState.Stopped,
                    _ => ServiceHostInstallState.Unknown,
                };

                Version? runningVersion = null;
                string runningProcessPath = string.Empty;
                if (state == ServiceHostInstallState.Running)
                {
                    (runningVersion, runningProcessPath) = await QueryRunningVersionAsync(cancellationToken).ConfigureAwait(false);
                }

                return new ServiceHostStatus
                {
                    State = state,
                    RawOutput = status.ToString(),
                    PackageExecutablePath = ServiceHostProtocol.PackageExecutablePath,
                    InstalledExecutablePath = ServiceHostProtocol.InstalledExecutablePath,
                    PackageVersion = packageVersion,
                    InstalledVersion = installedVersion,
                    RunningVersion = runningVersion,
                    RunningProcessPath = runningProcessPath,
                };
            }
            catch (InvalidOperationException ex)
            {
                log.Warn($"Service query failed: {ServiceHostProtocol.ServiceName}", ex);
                return new ServiceHostStatus
                {
                    State = ServiceHostInstallState.NotInstalled,
                    RawOutput = ex.Message,
                    PackageExecutablePath = ServiceHostProtocol.PackageExecutablePath,
                    InstalledExecutablePath = ServiceHostProtocol.InstalledExecutablePath,
                    PackageVersion = packageVersion,
                    InstalledVersion = installedVersion,
                };
            }
            catch (Exception ex)
            {
                log.Warn($"Service query failed: {ServiceHostProtocol.ServiceName}", ex);
                return new ServiceHostStatus
                {
                    State = ServiceHostInstallState.Unknown,
                    RawOutput = ex.Message,
                    PackageExecutablePath = ServiceHostProtocol.PackageExecutablePath,
                    InstalledExecutablePath = ServiceHostProtocol.InstalledExecutablePath,
                    PackageVersion = packageVersion,
                    InstalledVersion = installedVersion,
                };
            }
        }

        private static async Task<ServiceHostEnsureResult> InstallAndWaitForReadyAsync(
            TimeSpan timeout,
            ServiceHostStatus status,
            List<string> attempts,
            CancellationToken cancellationToken)
        {
            if (status.WouldInstallDowngrade)
            {
                return CreateEnsureFailure(
                    $"Refusing to install older service host package {status.PackageVersion}. Installed={status.InstalledVersion}, Running={status.RunningVersion}.",
                    status,
                    attempts);
            }

            ServiceHostOperationResult installResult = await InstallAsync(cancellationToken).ConfigureAwait(false);
            AddAttempt(attempts, "Install / Repair", installResult);
            if (!installResult.Success)
            {
                string installError = string.IsNullOrWhiteSpace(installResult.Error)
                    ? $"PowerShell exited with code {installResult.ExitCode}. See {Path.Combine(ServiceHostProtocol.InstallDirectory, "install.log")}."
                    : installResult.Error;
                return CreateEnsureFailure(
                    $"ColorVisionServiceHost installation failed: {installError}",
                    status,
                    attempts,
                    installResult.ExitCode);
            }

            ServiceHostStatus readyStatus = await WaitForReadyAsync(timeout, cancellationToken).ConfigureAwait(false);
            if (IsReadyForPackagedVersion(readyStatus))
                return CreateEnsureSuccess(readyStatus, attempts);

            return CreateEnsureFailure(
                $"ColorVisionServiceHost did not become ready within {timeout.TotalSeconds:0.#} seconds: {readyStatus.DisplayText}",
                readyStatus,
                attempts);
        }

        private static async Task<ServiceHostStatus> WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            ServiceHostStatus status = await QueryStatusAsync(cancellationToken).ConfigureAwait(false);
            while (!IsReadyForPackagedVersion(status) && DateTime.UtcNow < deadline)
            {
                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                await Task.Delay(remaining < ReadinessPollInterval ? remaining : ReadinessPollInterval, cancellationToken).ConfigureAwait(false);
                status = await QueryStatusAsync(cancellationToken).ConfigureAwait(false);
            }

            return status;
        }

        internal static bool IsReadyForPackagedVersion(ServiceHostStatus status)
        {
            return status.IsReady && !status.NeedsUpdate;
        }

        private static ServiceHostEnsureResult CreateEnsureSuccess(ServiceHostStatus status, List<string> attempts)
        {
            string readyMessage = $"ColorVisionServiceHost is ready: {status.DisplayText}";
            attempts.Add(readyMessage);
            return new ServiceHostEnsureResult
            {
                Success = true,
                ExitCode = 0,
                Details = string.Join(Environment.NewLine, attempts),
                Status = status,
            };
        }

        private static ServiceHostEnsureResult CreateEnsureFailure(
            string message,
            ServiceHostStatus status,
            List<string> attempts,
            int exitCode = -1)
        {
            return new ServiceHostEnsureResult
            {
                Success = false,
                ExitCode = exitCode,
                Details = string.Join(Environment.NewLine, attempts),
                Error = message,
                Status = status,
            };
        }

        private static void AddAttempt(List<string> attempts, string name, ServiceHostOperationResult result)
        {
            attempts.Add($"{name}: {result.Summary}");
        }

        private static async Task<(Version? Version, string ProcessPath)> QueryRunningVersionAsync(CancellationToken cancellationToken)
        {
            try
            {
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .StatusAsync(TimeSpan.FromSeconds(2), cancellationToken)
                    .ConfigureAwait(false);
                if (!response.Success || response.Data == null)
                    return (null, string.Empty);

                JToken data = response.Data;
                string? versionText = data["fileVersion"]?.ToString()
                    ?? data["productVersion"]?.ToString()
                    ?? data["assemblyVersion"]?.ToString();
                string processPath = data["processPath"]?.ToString() ?? string.Empty;
                return (TryParseVersion(versionText), processPath);
            }
            catch (Exception ex)
            {
                log.Warn("Failed to query running service host version.", ex);
                return (null, string.Empty);
            }
        }

        internal static string CreateInstallScript()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "$ErrorActionPreference = 'Stop'",
                $"$serviceName = {PsQuote(ServiceHostProtocol.ServiceName)}",
                $"$displayName = {PsQuote(ServiceHostProtocol.DisplayName)}",
                $"$description = {PsQuote(ServiceHostProtocol.Description)}",
                $"$source = {PsQuote(ServiceHostProtocol.PackageDirectory)}",
                $"$destination = {PsQuote(ServiceHostProtocol.InstallDirectory)}",
                $"$executableName = {PsQuote(ServiceHostProtocol.ExecutableName)}",
                "$logPath = Join-Path $destination 'install.log'",
                "function Write-Step([string]$message) { try { Add-Content -LiteralPath $logPath -Value (\"[$(Get-Date -Format o)] $message\") -Encoding UTF8 -ErrorAction Stop } catch {} }",
                "$serviceExistedBeforeInstall = $false",
                "New-Item -ItemType Directory -Force -Path $destination | Out-Null",
                "try {",
                "Write-Step \"Service host installation started. Source=$source Destination=$destination\"",
                "$sourceExe = Join-Path $source $executableName",
                "if (-not (Test-Path -LiteralPath $sourceExe)) { throw \"Service host executable was not found: $sourceExe\" }",
                "$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue",
                "$serviceExistedBeforeInstall = $null -ne $service",
                "if ($service -and $service.Status -ne 'Stopped') {",
                "    Stop-Service -Name $serviceName -Force -ErrorAction Stop",
                "    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))",
                "}",
                "Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force",
                "$exe = Join-Path $destination $executableName",
                "$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue",
                "if ($service) {",
                "    & sc.exe config $serviceName binPath= ('\"' + $exe + '\"') start= auto DisplayName= $displayName",
                "} else {",
                "    & sc.exe create $serviceName binPath= ('\"' + $exe + '\"') start= auto DisplayName= $displayName",
                "}",
                "if ($LASTEXITCODE -ne 0) { throw \"Failed to create or configure service: $LASTEXITCODE\" }",
                "& sc.exe description $serviceName $description",
                "if ($LASTEXITCODE -ne 0) { throw \"Failed to set service description: $LASTEXITCODE\" }",
                "& sc.exe start $serviceName",
                "if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1056) { throw \"Failed to start service: $LASTEXITCODE\" }",
                "Write-Step \"Service host installation completed. Executable=$exe\"",
                "Write-Output \"Service host installed to: $exe\"",
                "Write-Output \"Service start type: Automatic\"",
                "} catch {",
                "    Write-Step (\"Service host installation failed: \" + $_.Exception.Message)",
                "    if ($serviceExistedBeforeInstall) {",
                "        try {",
                "            $service = Get-Service -Name $serviceName -ErrorAction Stop",
                "            if ($service.Status -ne 'Running') {",
                "                Start-Service -Name $serviceName -ErrorAction Stop",
                "                $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(20))",
                "            }",
                "            Write-Step \"Service host restarted after failed installation.\"",
                "        } catch {",
                "            Write-Step (\"Failed to restart service host after installation failure: \" + $_.Exception.Message)",
                "        }",
                "    }",
                "    throw",
                "}",
                string.Empty,
            });
        }

        private static string CreateStartScript()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "$ErrorActionPreference = 'Stop'",
                $"$serviceName = {PsQuote(ServiceHostProtocol.ServiceName)}",
                "$service = Get-Service -Name $serviceName -ErrorAction Stop",
                "if ($service.Status -ne 'Running') {",
                "    Start-Service -Name $serviceName -ErrorAction Stop",
                "    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(20))",
                "}",
                "Write-Output \"Service host started.\"",
                string.Empty,
            });
        }

        private static string CreateStopScript()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "$ErrorActionPreference = 'Stop'",
                $"$serviceName = {PsQuote(ServiceHostProtocol.ServiceName)}",
                "$service = Get-Service -Name $serviceName -ErrorAction Stop",
                "if ($service.Status -ne 'Stopped') {",
                "    Stop-Service -Name $serviceName -Force -ErrorAction Stop",
                "    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))",
                "}",
                "Write-Output \"Service host stopped.\"",
                string.Empty,
            });
        }

        private static string CreateUninstallScript()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "$ErrorActionPreference = 'Stop'",
                $"$serviceName = {PsQuote(ServiceHostProtocol.ServiceName)}",
                $"$destination = {PsQuote(ServiceHostProtocol.InstallDirectory)}",
                "$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue",
                "if ($service) {",
                "    if ($service.Status -ne 'Stopped') {",
                "        Stop-Service -Name $serviceName -Force -ErrorAction Stop",
                "        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))",
                "    }",
                "    & sc.exe delete $serviceName",
                "    if ($LASTEXITCODE -ne 0) { throw \"Failed to delete service: $LASTEXITCODE\" }",
                "}",
                "if (Test-Path -LiteralPath $destination) {",
                "    Remove-Item -LiteralPath $destination -Recurse -Force",
                "}",
                "Write-Output \"Service host uninstalled.\"",
                string.Empty,
            });
        }

        private static async Task<ServiceHostOperationResult> RunPowerShellScriptAsync(string scriptContent, bool requireAdministrator, CancellationToken cancellationToken)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"ColorVisionServiceHost-{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(scriptPath, scriptContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            bool needsElevation = requireAdministrator && !IsAdministrator();
            ProcessStartInfo startInfo = new()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File {Quote(scriptPath)}",
                UseShellExecute = needsElevation,
                CreateNoWindow = !needsElevation,
                RedirectStandardOutput = !needsElevation,
                RedirectStandardError = !needsElevation,
            };

            if (needsElevation)
                startInfo.Verb = "runas";

            try
            {
                using Process? process = Process.Start(startInfo);
                if (process == null)
                {
                    return new ServiceHostOperationResult
                    {
                        Success = false,
                        ExitCode = -1,
                        Error = "Failed to start elevated service host deployment.",
                    };
                }

                string output = string.Empty;
                string error = string.Empty;
                if (!needsElevation)
                {
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                    Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    output = await outputTask.ConfigureAwait(false);
                    error = await errorTask.ConfigureAwait(false);
                }
                else
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }

                ServiceHostOperationResult result = new()
                {
                    Success = process.ExitCode == 0,
                    ExitCode = process.ExitCode,
                    Output = output,
                    Error = error,
                };
                log.Info($"powershell.exe service host deployment: {result.Summary}");
                return result;
            }
            catch (Exception ex)
            {
                log.Error("Failed to run service host deployment script.", ex);
                return new ServiceHostOperationResult
                {
                    Success = false,
                    ExitCode = -1,
                    Error = ex.Message,
                };
            }
            finally
            {
                try
                {
                    File.Delete(scriptPath);
                }
                catch
                {
                }
            }
        }

        private static string Quote(string value)
        {
            return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        }

        private static string PsQuote(string value)
        {
            return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
        }

        private static Version? GetExecutableVersion(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(filePath);
            return TryParseVersion(info.FileVersion) ?? TryParseVersion(info.ProductVersion);
        }

        private static Version? TryParseVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string versionText = new(value.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray());
            return Version.TryParse(versionText.Trim('.'), out Version? version) ? version : null;
        }
    }
}
