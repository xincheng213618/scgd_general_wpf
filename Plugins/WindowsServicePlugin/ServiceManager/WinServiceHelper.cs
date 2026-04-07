using log4net;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace WindowsServicePlugin.ServiceManager
{
    public static class WinServiceHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WinServiceHelper));

        public static bool IsServiceExisted(string serviceName)
        {
            try
            {
                ServiceController[] services = ServiceController.GetServices();
                foreach (var svc in services)
                {
                    if (string.Equals(svc.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception ex)
            {
                log.Error($"检查服务 {serviceName} 是否存在时出错", ex);
            }
            return false;
        }

        public static ServiceControllerStatus GetServiceStatus(string serviceName)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                return sc.Status;
            }
            catch
            {
                return ServiceControllerStatus.Stopped;
            }
        }

        public static bool IsServiceRunning(string serviceName)
        {
            return GetServiceStatus(serviceName) == ServiceControllerStatus.Running;
        }

        public static bool IsServiceStopped(string serviceName)
        {
            return GetServiceStatus(serviceName) == ServiceControllerStatus.Stopped;
        }

        public static bool StartService(string serviceName, int timeoutSeconds = 30)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running) return true;
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    sc.Start();
                }
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(timeoutSeconds));
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch (Exception ex)
            {
                log.Error($"启动服务 {serviceName} 失败", ex);
                return false;
            }
        }

        public static bool StopService(string serviceName, int timeoutSeconds = 30)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Stopped) return true;
                if (sc.CanStop)
                {
                    sc.Stop();
                }
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeoutSeconds));
                return sc.Status == ServiceControllerStatus.Stopped;
            }
            catch (Exception ex)
            {
                log.Error($"停止服务 {serviceName} 失败", ex);
                return false;
            }
        }

        public static bool InstallService(string serviceName, string exePath)
        {
            if (!File.Exists(exePath)) return false;
            try
            {
                if (IsServiceExisted(serviceName))
                {
                    log.Info($"服务已存在，跳过创建: {serviceName}");
                    return true;
                }

                // 优先使用服务程序自带安装参数，兼容其内部固定服务名。
                if (ExecuteFileAsAdmin(exePath, "--install") && IsServiceExisted(serviceName))
                {
                    return true;
                }

                // 回退到 sc create，兼容没有 --install 参数的服务程序。
                string arguments = $"create \"{serviceName}\" binPath= \"{exePath}\" start= auto";
                bool created = ExecuteScCommand(arguments, true);
                if (created && IsServiceExisted(serviceName))
                {
                    return true;
                }

                ExecuteFileAsAdmin(exePath, "/install");
                return IsServiceExisted(serviceName);
            }
            catch (Exception ex)
            {
                log.Error($"安装服务失败 {serviceName} => {exePath}", ex);
                return false;
            }
        }

        public static bool UninstallService(string serviceName)
        {
            try
            {
                if (IsServiceExisted(serviceName))
                {
                    try
                    {
                        StopService(serviceName, 10);
                    }
                    catch
                    {
                    }
                }
                return ExecuteScCommand($"delete {serviceName}", true);
            }
            catch (Exception ex)
            {
                log.Error($"卸载服务 {serviceName} 失败", ex);
                return false;
            }
        }

        public static bool ExecuteScCommand(string arguments, bool runAsAdmin = false)
        {
            try
            {
                bool needElevation = runAsAdmin && !ColorVision.Common.Utilities.Tool.IsAdministrator();
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    UseShellExecute = needElevation,
                    Verb = needElevation ? "runas" : null,
                    RedirectStandardOutput = !needElevation,
                    RedirectStandardError = !needElevation,
                    CreateNoWindow = !needElevation
                };
                using var process = Process.Start(psi);
                string output = needElevation ? string.Empty : process?.StandardOutput.ReadToEnd() ?? "";
                string err = needElevation ? string.Empty : process?.StandardError.ReadToEnd() ?? "";
                process?.WaitForExit(15000);
                log.Info($"sc {arguments}: exit={(process?.ExitCode ?? -1)} {output.Trim()} {err.Trim()}");
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                log.Error($"执行 sc {arguments} 失败", ex);
                return false;
            }
        }

        public static bool ExecuteFileAsAdmin(string fileName, string arguments, int timeoutMilliseconds = 30000)
        {
            try
            {
                bool needElevation = !ColorVision.Common.Utilities.Tool.IsAdministrator();
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = needElevation,
                    Verb = needElevation ? "runas" : null,
                    CreateNoWindow = true,
                    RedirectStandardOutput = !needElevation,
                    RedirectStandardError = !needElevation
                };
                using var process = Process.Start(psi);
                if (!needElevation)
                {
                    _ = process?.StandardOutput.ReadToEnd();
                    _ = process?.StandardError.ReadToEnd();
                }
                process?.WaitForExit(timeoutMilliseconds);
                log.Info($"执行提权程序: {fileName} {arguments}, exit={(process?.ExitCode ?? -1)}");
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                log.Error($"执行提权程序失败: {fileName} {arguments}", ex);
                return false;
            }
        }

        public static string? GetServiceInstallPath(string serviceName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                if (key != null)
                {
                    var imagePath = key.GetValue("ImagePath")?.ToString();
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        // 去除引号和参数
                        imagePath = imagePath.Trim('"');
                        var match = Regex.Match(imagePath, @"^""?([^""]+\.exe)""?");
                        if (match.Success)
                            return match.Groups[1].Value;
                        var parts = imagePath.Split(' ');
                        return parts[0].Trim('"');
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"获取服务路径 {serviceName} 失败", ex);
            }
            return null;
        }

        public static Version? GetFileVersion(string exePath)
        {
            if (!File.Exists(exePath)) return null;
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                    return new Version(versionInfo.FileVersion);
            }
            catch { }
            return null;
        }

        public static void KillProcessByName(string processName)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(5000);
                        log.Info($"已终止进程 {processName} (PID: {p.Id})");
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"终止进程失败 {processName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"查找进程 {processName} 失败", ex);
            }
        }

        public static bool ExecuteCommand(string command, bool asAdmin = true)
        {
            try
            {
                bool needElevation = asAdmin && !ColorVision.Common.Utilities.Tool.IsAdministrator();
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command}",
                    UseShellExecute = needElevation,
                    CreateNoWindow = !needElevation,
                    RedirectStandardOutput = !needElevation,
                    Verb = needElevation ? "runas" : null
                };
                using var process = Process.Start(psi);
                if (!needElevation)
                {
                    string output = process?.StandardOutput.ReadToEnd() ?? "";
                    log.Info(output);
                }
                process?.WaitForExit(60000);
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                log.Error($"执行命令失败: {command}", ex);
                return false;
            }
        }
    }
}
