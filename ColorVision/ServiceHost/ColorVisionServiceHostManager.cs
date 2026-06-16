using log4net;
using System;
using System.Diagnostics;
using System.IO;
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
        public ServiceHostInstallState State { get; init; }

        public string RawOutput { get; init; } = string.Empty;

        public string DisplayText => State switch
        {
            ServiceHostInstallState.NotInstalled => "Not installed",
            ServiceHostInstallState.Stopped => "Stopped",
            ServiceHostInstallState.Running => "Running",
            _ => "Unknown",
        };
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

    public static class ColorVisionServiceHostManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ColorVisionServiceHostManager));

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

        public static Task<ServiceHostStatus> QueryStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using ServiceController controller = new(ServiceHostProtocol.ServiceName);
                ServiceControllerStatus status = controller.Status;
                return Task.FromResult(new ServiceHostStatus
                {
                    State = status switch
                    {
                        ServiceControllerStatus.Running => ServiceHostInstallState.Running,
                        ServiceControllerStatus.Stopped => ServiceHostInstallState.Stopped,
                        _ => ServiceHostInstallState.Unknown,
                    },
                    RawOutput = status.ToString(),
                });
            }
            catch (InvalidOperationException ex)
            {
                log.Warn($"Service query failed: {ServiceHostProtocol.ServiceName}", ex);
                return Task.FromResult(new ServiceHostStatus
                {
                    State = ServiceHostInstallState.NotInstalled,
                    RawOutput = ex.Message,
                });
            }
            catch (Exception ex)
            {
                log.Warn($"Service query failed: {ServiceHostProtocol.ServiceName}", ex);
                return Task.FromResult(new ServiceHostStatus
                {
                    State = ServiceHostInstallState.Unknown,
                    RawOutput = ex.Message,
                });
            }
        }

        private static string CreateInstallScript()
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
                "$sourceExe = Join-Path $source $executableName",
                "if (-not (Test-Path -LiteralPath $sourceExe)) { throw \"Service host executable was not found: $sourceExe\" }",
                "$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue",
                "if ($service -and $service.Status -ne 'Stopped') {",
                "    Stop-Service -Name $serviceName -Force -ErrorAction Stop",
                "    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))",
                "}",
                "New-Item -ItemType Directory -Force -Path $destination | Out-Null",
                "& icacls $destination /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null",
                "if ($LASTEXITCODE -ne 0) { throw \"Failed to set service host directory ACL: $LASTEXITCODE\" }",
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
                "Write-Output \"Service host installed to: $exe\"",
                "Write-Output \"Service start type: Automatic\"",
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
    }
}
