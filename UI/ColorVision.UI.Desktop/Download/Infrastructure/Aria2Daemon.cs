using log4net;
using ColorVision.Update;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;

namespace ColorVision.UI.Desktop.Download
{
    internal sealed class Aria2Daemon : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(Aria2Daemon));
        private readonly object _processLock = new();
        private readonly string _aria2cPath;
        private Process? _process;
        private bool? _disableSystemProxy;

        public Aria2Daemon()
        {
            _aria2cPath = FindAria2c();
        }

        public bool IsRunning
        {
            get
            {
                lock (_processLock)
                {
                    return _process != null && !_process.HasExited;
                }
            }
        }

        public bool IsRunningForNetworkMode(bool disableSystemProxy)
        {
            lock (_processLock)
            {
                return _process != null
                    && !_process.HasExited
                    && _disableSystemProxy == disableSystemProxy;
            }
        }

        public int PreparePort(int port)
        {
            if (!IsPortInUse(port))
                return port;

            KillOrphanProcesses();
            if (!IsPortInUse(port))
                return port;

            return FindAvailablePort(port + 1);
        }

        public void Start(int port, string rpcSecret, DownloadManagerConfig config, bool disableSystemProxy)
        {
            lock (_processLock)
            {
                if (_process != null && !_process.HasExited)
                    return;

                var psi = new ProcessStartInfo
                {
                    FileName = _aria2cPath,
                    Arguments = BuildArguments(port, rpcSecret, config, disableSystemProxy),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                UpdateNetworkConfig.ConfigureChildProcessProxyEnvironment(psi, disableSystemProxy);
                log.Info(disableSystemProxy
                    ? "Starting aria2c with inherited proxy environment disabled."
                    : "Starting aria2c with inherited proxy environment enabled.");

                _process = new Process { StartInfo = psi };
                _process.Start();
                _disableSystemProxy = disableSystemProxy;

                // Discard output to prevent buffer deadlock.
                _process.StandardOutput.ReadToEndAsync();
                _process.StandardError.ReadToEndAsync();
            }
        }

        public void Stop(Action forceShutdown)
        {
            Process? processToStop;
            lock (_processLock)
            {
                processToStop = _process;
                _process = null;
                _disableSystemProxy = null;
            }

            try
            {
                if (processToStop != null)
                {
                    try
                    {
                        forceShutdown();
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"RPC force shutdown failed: {ex.Message}");
                    }
                }

                if (processToStop != null && !processToStop.HasExited)
                {
                    if (!processToStop.WaitForExit(2000))
                    {
                        processToStop.Kill(entireProcessTree: true);
                        processToStop.WaitForExit(2000);
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                log.Debug($"Stop aria2c daemon failed: {ex.Message}");
            }
            finally
            {
                processToStop?.Dispose();
            }
        }

        public void Dispose()
        {
            Process? processToDispose;
            lock (_processLock)
            {
                processToDispose = _process;
                _process = null;
                _disableSystemProxy = null;
            }

            processToDispose?.Dispose();
        }

        private static string BuildArguments(int port, string rpcSecret, DownloadManagerConfig config, bool disableSystemProxy)
        {
            string args = $"--enable-rpc --rpc-listen-port={port} --rpc-secret={rpcSecret} --rpc-listen-all=false --enable-color=false -c --auto-file-renaming=true --allow-overwrite=false --summary-interval=0 -j {config.MaxConcurrentTasks}" +
                $" --enable-dht=true --bt-enable-lpd=true --enable-peer-exchange=true --follow-torrent=mem --seed-time=0 --bt-save-metadata=true --stop-with-process={Environment.ProcessId}";

            if (disableSystemProxy)
                args += " --all-proxy= --http-proxy= --https-proxy= --ftp-proxy=";

            if (config.EnableSpeedLimit)
            {
                args += $" --max-overall-download-limit={config.SpeedLimitMB}M";
            }

            return args;
        }

        private static string FindAria2c()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string aria2cInTool = Path.Combine(appDir, "Assets", "Tool", "aria2c.exe");
            if (File.Exists(aria2cInTool)) return aria2cInTool;

            string aria2cInRoot = Path.Combine(appDir, "aria2c.exe");
            if (File.Exists(aria2cInRoot)) return aria2cInRoot;

            return "aria2c";
        }

        private void KillOrphanProcesses()
        {
            try
            {
                string? ourAria2cDir = Path.GetDirectoryName(_aria2cPath);
                var aria2cProcesses = Process.GetProcessesByName("aria2c");
                foreach (var proc in aria2cProcesses)
                {
                    try
                    {
                        string? procPath = null;
                        try { procPath = proc.MainModule?.FileName; } catch { }

                        if (procPath != null && ourAria2cDir != null &&
                            Path.GetDirectoryName(procPath)?.Equals(ourAria2cDir, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            proc.Kill();
                            proc.WaitForExit(2000);
                            log.Info($"Killed orphan aria2c process (PID: {proc.Id})");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"Failed to kill aria2c process {proc.Id}: {ex.Message}");
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"KillOrphanProcesses failed: {ex.Message}");
            }
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var listeners = ipProperties.GetActiveTcpListeners();
                return listeners.Any(ep => ep.Port == port);
            }
            catch (Exception ex)
            {
                log.Debug($"Port check failed: {ex.Message}");
                return false;
            }
        }

        private static int FindAvailablePort(int startPort)
        {
            for (int port = startPort; port < startPort + 100; port++)
            {
                if (!IsPortInUse(port))
                    return port;
            }
            return startPort;
        }
    }
}
