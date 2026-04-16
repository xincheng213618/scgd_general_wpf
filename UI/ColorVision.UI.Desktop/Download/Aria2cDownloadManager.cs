using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Desktop.Download
{

    public class Aria2cDownloadManager : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(Aria2cDownloadManager));
        private static readonly string[] TellStatusKeys = { "status", "totalLength", "completedLength", "downloadSpeed", "errorCode", "errorMessage", "bittorrent", "files" };
        private const int PollIntervalMs = 300;
        private const int LocalCopyBufferSize = 1024 * 1024;
        private static readonly TimeSpan LocalCopyProgressInterval = TimeSpan.FromMilliseconds(150);

        private static Aria2cDownloadManager? _instance;
        private static readonly object _locker = new();

        public static Aria2cDownloadManager GetInstance()
        {
            lock (_locker)
            {
                _instance ??= new Aria2cDownloadManager();
                return _instance;
            }
        }

        public static string DirectoryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Downloads");
        public static string DbPath { get; set; } = Path.Combine(DirectoryPath, "Downloads.db");

        public ObservableCollection<DownloadTask> Tasks { get; } = new();
        private readonly ConcurrentDictionary<int, DownloadTask> _activeTasks = new();
        private readonly ConcurrentDictionary<int, DownloadTask> _localCopyTasks = new();
        private readonly string _aria2cPath;

        // JSON-RPC state
        private Process? _aria2cProcess;
        private readonly object _processLock = new();
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
        private int _rpcPort = 6800;
        private const string RpcSecret = "ColorVisionDL";
        private string RpcUrl => $"http://127.0.0.1:{_rpcPort}/jsonrpc";
        private int _rpcRequestId;
        private Timer? _pollTimer;
        private int _disposeState;
        private int _isPollCallback;
        /// <summary>
        /// Tracks whether we are connected to a reused (orphan) aria2c process
        /// </summary>
        private bool _reusingExistingAria2c;

        /// <summary>
        /// Current RPC port used by the aria2c daemon
        /// </summary>
        public int CurrentRpcPort => _rpcPort;

        /// <summary>
        /// Status message for the download service
        /// </summary>
        public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; StatusMessageChanged?.Invoke(this, value); } }
        private string _statusMessage = string.Empty;

        /// <summary>
        /// Fired when the status message changes
        /// </summary>
        public event EventHandler<string>? StatusMessageChanged;

        /// <summary>
        /// Fired when a download task completes (success or failure)
        /// </summary>
        public event EventHandler<DownloadTask>? DownloadCompleted;

        public DownloadManagerConfig Config => DownloadManagerConfig.Instance;
        private bool IsDisposingOrDisposed => Volatile.Read(ref _disposeState) != 0;

        private Aria2cDownloadManager()
        {
            Directory.CreateDirectory(DirectoryPath);
            _aria2cPath = FindAria2c();
            InitializeDatabase();

            // Use configured port
            _rpcPort = Config.RpcPort;

            // Subscribe to config changes for live updates
            Config.PropertyChanged += OnConfigPropertyChanged;

            if (Application.Current != null)
            {
                Application.Current.Exit += OnApplicationExit;
                Application.Current.SessionEnding += OnApplicationSessionEnding;
            }

            // ProcessExit is kept as a last-resort fallback when WPF shutdown is bypassed.
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private void OnApplicationExit(object? sender, ExitEventArgs e) => Dispose();

        private void OnApplicationSessionEnding(object? sender, SessionEndingCancelEventArgs e) => Dispose();

        private void OnProcessExit(object? sender, EventArgs e) => Dispose();

        /// <summary>
        /// Cleanly shut down the aria2c daemon and release resources.
        /// Called automatically on process exit.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
                return;

            try { Config.PropertyChanged -= OnConfigPropertyChanged; }
            catch { }

            if (Application.Current != null)
            {
                try { Application.Current.Exit -= OnApplicationExit; } catch { }
                try { Application.Current.SessionEnding -= OnApplicationSessionEnding; } catch { }
            }

            try { AppDomain.CurrentDomain.ProcessExit -= OnProcessExit; }
            catch { }

            StopAria2cDaemon();
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handle config property changes and apply them to the running aria2c instance via RPC
        /// </summary>
        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DownloadManagerConfig.EnableSpeedLimit):
                case nameof(DownloadManagerConfig.SpeedLimitMB):
                    ApplySpeedLimitAsync();
                    break;
                case nameof(DownloadManagerConfig.MaxConcurrentTasks):
                    ApplyMaxConcurrentTasksAsync();
                    break;
            }
        }

        private void ApplySpeedLimitAsync()
        {
            Task.Run(async () =>
            {
                try
                {
                    if (IsDisposingOrDisposed) return;
                    if (!IsAria2cRunning) return;
                    string limit = Config.EnableSpeedLimit ? $"{Config.SpeedLimitMB}M" : "0";
                    var options = new Dictionary<string, string> { ["max-overall-download-limit"] = limit };
                    await RpcCallAsync("aria2.changeGlobalOption", new object[] { $"token:{RpcSecret}", options });
                    Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = Properties.Resources.ConfigApplied);
                    log.Info($"Speed limit applied: {limit}");
                }
                catch (Exception ex)
                {
                    log.Debug($"Failed to apply speed limit: {ex.Message}");
                }
            });
        }

        private void ApplyMaxConcurrentTasksAsync()
        {
            Task.Run(async () =>
            {
                try
                {
                    if (IsDisposingOrDisposed) return;
                    if (!IsAria2cRunning) return;
                    var options = new Dictionary<string, string> { ["max-concurrent-downloads"] = Config.MaxConcurrentTasks.ToString() };
                    await RpcCallAsync("aria2.changeGlobalOption", new object[] { $"token:{RpcSecret}", options });
                    Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = Properties.Resources.ConfigApplied);
                    log.Info($"Max concurrent tasks applied: {Config.MaxConcurrentTasks}");
                }
                catch (Exception ex)
                {
                    log.Debug($"Failed to apply max concurrent tasks: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Whether we have a working connection to an aria2c daemon (own process or reused)
        /// </summary>
        public bool IsAria2cRunning
        {
            get
            {
                lock (_processLock)
                {
                    if (_aria2cProcess != null && !_aria2cProcess.HasExited)
                        return true;
                }
                return _reusingExistingAria2c;
            }
        }

        /// <summary>
        /// Pre-load the aria2c daemon so it's ready when downloads are created.
        /// Called when the DownloadWindow opens.
        /// </summary>
        public void PreloadAria2cAsync()
        {
            if (IsDisposingOrDisposed)
                return;

            Task.Run(async () =>
            {
                try
                {
                    if (IsDisposingOrDisposed) return;
                    await EnsureAria2cRunningAsync();
                }
                catch (Exception ex)
                {
                    log.Debug($"Preload aria2c failed: {ex.Message}");
                }
            });
        }

        private string FindAria2c()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string aria2cInTool = Path.Combine(appDir, "Assets", "Tool", "aria2c.exe");
            if (File.Exists(aria2cInTool)) return aria2cInTool;

            string aria2cInRoot = Path.Combine(appDir, "aria2c.exe");
            if (File.Exists(aria2cInRoot)) return aria2cInRoot;

            return "aria2c";
        }

        private void InitializeDatabase()
        {
            using var db = CreateDbClient();
            db.CodeFirst.InitTables<DownloadEntry>();
        }

        public static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        private sealed class RemoteFileValidationInfo
        {
            public long? ContentLength { get; init; }
            public string? ETag { get; init; }
            public DateTimeOffset? LastModified { get; init; }
        }

        #region aria2c RPC Daemon

        private async Task EnsureAria2cRunningAsync()
        {
            if (IsDisposingOrDisposed)
                return;

            if (_reusingExistingAria2c)
                return;

            lock (_processLock)
            {
                if (_aria2cProcess != null && !_aria2cProcess.HasExited)
                    return;
            }

            await StartAria2cDaemonAsync();
        }

        private async Task StartAria2cDaemonAsync()
        {
            if (IsDisposingOrDisposed)
                return;

            lock (_processLock)
            {
                if (_aria2cProcess != null && !_aria2cProcess.HasExited)
                    return;
            }

            // If the configured port is in use, try to connect to an existing aria2c (orphan from previous session)
            if (IsPortInUse(_rpcPort))
            {
                //bool reused = await TryReuseExistingAria2cAsync();
                //if (reused)
                //{
                //    _reusingExistingAria2c = true;
                //    log.Info($"Reusing existing aria2c on port {_rpcPort}");
                //    UpdateServiceStatus();
                //    StartPolling();
                //    return;
                //}

                // Could not reuse - kill any orphan aria2c processes on our port range, then find a new port
                KillOrphanAria2cProcesses();
                if (IsPortInUse(_rpcPort))
                {
                    int newPort = FindAvailablePort(_rpcPort + 1);
                    log.Warn($"Port {_rpcPort} is in use, switching to port {newPort}");
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        StatusMessage = string.Format(Properties.Resources.PortSwitched, _rpcPort, newPort);
                    });
                    _rpcPort = newPort;
                }
            }

            lock (_processLock)
            {
                if (_aria2cProcess != null && !_aria2cProcess.HasExited)
                    return;

                string args = $"--enable-rpc --rpc-listen-port={_rpcPort} --rpc-secret={RpcSecret} --rpc-listen-all=false --enable-color=false -c --auto-file-renaming=true --allow-overwrite=false --summary-interval=0 -j {Config.MaxConcurrentTasks}" +
                    $" --enable-dht=true --bt-enable-lpd=true --enable-peer-exchange=true --follow-torrent=mem --seed-time=0 --bt-save-metadata=true --stop-with-process={Environment.ProcessId}";

                if (Config.EnableSpeedLimit)
                {
                    args += $" --max-overall-download-limit={Config.SpeedLimitMB}M";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = _aria2cPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _aria2cProcess = new Process { StartInfo = psi };
                _aria2cProcess.Start();

                // Discard output to prevent buffer deadlock
                _aria2cProcess.StandardOutput.ReadToEndAsync();
                _aria2cProcess.StandardError.ReadToEndAsync();
            }

            // Wait for RPC to be ready
            for (int i = 0; i < 30; i++)
            {
                if (IsDisposingOrDisposed)
                    return;

                await Task.Delay(100).ConfigureAwait(false);
                try
                {
                    var response = await RpcCallAsync("aria2.getVersion", new object[] { $"token:{RpcSecret}" }).ConfigureAwait(false);
                    if (response?["result"] != null)
                    {
                        log.Info("aria2c RPC daemon started successfully");
                        UpdateServiceStatus();
                        StartPolling();
                        return;
                    }
                }
                catch { }
            }

            log.Error("Failed to start aria2c RPC daemon");
            StatusMessage = Properties.Resources.Aria2cStartFailed;
            throw new Exception("Failed to start aria2c RPC daemon");
        }

        /// <summary>
        /// Try to connect to an existing aria2c instance on the current port.
        /// Returns true if we can reuse it (responds to our RPC secret).
        /// </summary>
        private async Task<bool> TryReuseExistingAria2cAsync()
        {
            try
            {
                var response = await RpcCallAsync("aria2.getVersion", new object[] { $"token:{RpcSecret}" });
                return response?["result"] != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kill orphan aria2c processes that may have been left from a previous session.
        /// Only kills processes whose executable path matches our bundled aria2c.
        /// </summary>
        private void KillOrphanAria2cProcesses()
        {
            try
            {
                string? ourAria2cDir = Path.GetDirectoryName(_aria2cPath);
                var aria2cProcesses = Process.GetProcessesByName("aria2c");
                foreach (var proc in aria2cProcesses)
                {
                    try
                    {
                        // Only kill if it's our bundled aria2c (same directory)
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
                log.Debug($"KillOrphanAria2cProcesses failed: {ex.Message}");
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
            catch (Exception ex) { log.Debug($"Port check failed: {ex.Message}"); return false; }
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

        private void StopAria2cDaemon()
        {
            bool reusedExistingAria2c = _reusingExistingAria2c;
            _reusingExistingAria2c = false;
            StopPolling();

            Process? processToStop;
            lock (_processLock)
            {
                processToStop = _aria2cProcess;
                _aria2cProcess = null;
            }

            try
            {
                if (reusedExistingAria2c || processToStop != null)
                {
                    try
                    {
                        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                        RpcCallAsync("aria2.forceShutdown", new object[] { $"token:{RpcSecret}" }, cancellationTokenSource.Token)
                            .GetAwaiter()
                            .GetResult();
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
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                log.Debug($"Stop aria2c daemon failed: {ex.Message}");
            }
            finally
            {
                processToStop?.Dispose();
            }

            UpdateServiceStatus();
        }

        /// <summary>
        /// Update the service status message with connection state and port
        /// </summary>
        private void UpdateServiceStatus()
        {
            bool running = IsAria2cRunning;
            string connText = running ? Properties.Resources.Aria2cConnected : Properties.Resources.Aria2cDisconnected;
            string status = string.Format(Properties.Resources.Aria2cServiceStatus, connText, _rpcPort);
            Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = status);
        }

        private void StartPolling()
        {
            if (IsDisposingOrDisposed)
                return;

            _pollTimer ??= new Timer(PollCallback, null, 0, PollIntervalMs);
        }

        private void StopPolling()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        private async void PollCallback(object? state)
        {
            if (Interlocked.Exchange(ref _isPollCallback, 1) == 1)
                return;

            try
            {
                if (IsDisposingOrDisposed)
                    return;

                var activeTasks = _activeTasks.Values.ToArray();
                if (activeTasks.Length == 0)
                {
                    // No active downloads — only stop the polling timer, keep aria2c process alive
                    // for instant reuse when new downloads are added (avoids slow restart)
                    StopPolling();
                    UpdateServiceStatus();
                    return;
                }

                // Get global stats for overall speed display
                long globalSpeed = 0;
                try
                {
                    var globalStat = await RpcCallAsync("aria2.getGlobalStat", new object[] { $"token:{RpcSecret}" });
                    if (globalStat != null)
                    {
                        globalSpeed = ParseLong(globalStat["result"]?["downloadSpeed"]);
                    }
                }
                catch { }

                string statusText = string.Format(Properties.Resources.ActiveDownloads, activeTasks.Length);
                if (globalSpeed > 0)
                    statusText += " | " + string.Format(Properties.Resources.GlobalSpeed, DownloadTask.FormatSpeed(globalSpeed));
                StatusMessage = statusText;
                foreach (var task in activeTasks)
                {
                    if (string.IsNullOrEmpty(task.Gid)) continue;

                    try
                    {
                        var status = await RpcCallAsync("aria2.tellStatus",
                            new object[] { $"token:{RpcSecret}", task.Gid, TellStatusKeys });

                        if (status == null) continue;

                        string? rpcStatus = status["result"]?["status"]?.ToString();
                        long totalLength = ParseLong(status["result"]?["totalLength"]);
                        long completedLength = ParseLong(status["result"]?["completedLength"]);
                        long downloadSpeed = ParseLong(status["result"]?["downloadSpeed"]);

                        // Update file name from BT metadata if available
                        var btInfo = status["result"]?["bittorrent"]?["info"]?["name"]?.ToString();
                        if (!string.IsNullOrEmpty(btInfo) && task.FileName != btInfo)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() => task.FileName = btInfo);
                            UpdateEntryFileName(task.Id, btInfo);
                        }

                        int progress = totalLength > 0 ? (int)(completedLength * 100 / totalLength) : 0;
                        string speedText = DownloadTask.FormatSpeed(downloadSpeed);

                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            task.ProgressValue = progress;
                            task.TotalBytes = totalLength;
                            task.DownloadedBytes = completedLength;
                            task.SpeedText = speedText;
                        });

                        if (rpcStatus == "complete")
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                task.TotalBytes = totalLength;
                                task.Status = DownloadStatus.Completed;
                                task.SpeedText = string.Empty;
                            });
                            UpdateEntryCompleted(task);
                            _activeTasks.TryRemove(task.Id, out _);
                            task.OnCompletedCallback?.Invoke(task);
                            DownloadCompleted?.Invoke(this, task);
                        }
                        else if (rpcStatus == "error")
                        {
                            string? errorMsg = status["result"]?["errorMessage"]?.ToString();
                            string? errorCode = status["result"]?["errorCode"]?.ToString();

                            // errorCode "15" = "no URI" / no url available — often caused by stale .aria2 cache
                            if (errorCode == "15" && TryClearStaleCacheFiles(task.SavePath))
                            {
                                log.Info($"Cleared stale cache for task {task.Id} ({task.FileName}), auto-retrying");
                                _activeTasks.TryRemove(task.Id, out _);
                                task.Gid = null;
                                _ = StartDownloadAsync(task);
                            }
                            else
                            {
                                Application.Current?.Dispatcher.BeginInvoke(() =>
                                {
                                    task.Status = DownloadStatus.Failed;
                                    task.ErrorMessage = errorMsg ?? "Unknown error";
                                    task.SpeedText = string.Empty;
                                });
                                UpdateEntryStatus(task.Id, DownloadStatus.Failed, errorMsg);
                                _activeTasks.TryRemove(task.Id, out _);
                                task.OnCompletedCallback?.Invoke(task);
                                DownloadCompleted?.Invoke(this, task);
                            }
                        }
                        else if (rpcStatus == "removed" || rpcStatus == "paused")
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                task.Status = DownloadStatus.Paused;
                                task.SpeedText = string.Empty;
                            });
                            UpdateEntryStatus(task.Id, DownloadStatus.Paused);
                            _activeTasks.TryRemove(task.Id, out _);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"Poll status error for task {task.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Poll callback error: {ex.Message}");
            }
            finally
            {
                Volatile.Write(ref _isPollCallback, 0);
            }
        }

        private async Task<JObject?> RpcCallAsync(string method, object[] parameters, CancellationToken cancellationToken = default)
        {
            int id = Interlocked.Increment(ref _rpcRequestId);
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id.ToString(),
                ["method"] = method,
                ["params"] = JToken.FromObject(parameters)
            };

            string json = request.ToString(Formatting.None);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(RpcUrl, content, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JObject.Parse(responseBody);
        }

        private static long ParseLong(JToken? token)
        {
            return long.TryParse(token?.ToString(), out long value) ? value : 0;
        }

        #endregion

        public void AutoRestartIncompleteDownloadsAsync()
        {
            if (IsDisposingOrDisposed)
                return;

            Task.Run(() => AutoRestartIncompleteDownloads());
        }

        /// <summary>
        /// Auto-restart incomplete downloads (Waiting, Downloading status) from previous session
        /// </summary>
        public void AutoRestartIncompleteDownloads()
        {
            try
            {
                if (IsDisposingOrDisposed)
                    return;

                using var db = CreateDbClient();
                var incompleteEntries = db.Queryable<DownloadEntry>()
                    .Where(x => x.Status == (int)DownloadStatus.Waiting || x.Status == (int)DownloadStatus.Downloading || x.Status == (int)DownloadStatus.Paused)
                    .ToList();

                if (incompleteEntries.Count == 0) return;

                log.Info($"Auto-restarting {incompleteEntries.Count} incomplete downloads");
                StatusMessage = string.Format(Properties.Resources.AutoRestartingDownloads, incompleteEntries.Count);

                foreach (var entry in incompleteEntries)
                {
                    var task = new DownloadTask
                    {
                        Id = entry.Id,
                        Url = entry.Url,
                        FileName = entry.FileName,
                        SavePath = entry.SavePath,
                        Status = DownloadStatus.Waiting,
                        CreateTime = entry.CreateTime,
                        Authorization = DecodeAuth(entry.Authorization)
                    };

                    _activeTasks.AddOrUpdate(task.Id, task, (key, old) => task);
                    Application.Current?.Dispatcher.BeginInvoke(() => Tasks.Insert(0, task));
                    _ = StartDownloadAsync(task);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Auto-restart incomplete downloads failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Add a download task with default settings
        /// </summary>
        public DownloadTask AddDownload(string url, string? savePath = null, string? authorization = null, Action<DownloadTask>? onCompleted = null, string? fileName = null)
        {
            string targetDir = savePath ?? Config.DefaultDownloadPath;
            Directory.CreateDirectory(targetDir);

            fileName ??= GetFileNameFromUrl(url);
            string preferredPath = Path.Combine(targetDir, fileName);
            string filePath = GetUniqueFilePath(targetDir, fileName);
            fileName = Path.GetFileName(filePath);
            DownloadEntry? reusableEntry = null;

            if (!string.Equals(filePath, preferredPath, StringComparison.OrdinalIgnoreCase))
            {
                reusableEntry = FindReusableCompletedEntry(url, targetDir, filePath, preferredPath);
            }

            var entry = new DownloadEntry
            {
                Url = url,
                FileName = fileName,
                SavePath = filePath,
                Status = (int)DownloadStatus.Waiting,
                CreateTime = DateTime.Now,
                Authorization = EncodeAuth(authorization)
            };

            using (var db = CreateDbClient())
            {
                entry.Id = db.Insertable(entry).ExecuteReturnIdentity();
            }

            var task = new DownloadTask
            {
                Id = entry.Id,
                Url = url,
                FileName = fileName,
                SavePath = filePath,
                Status = DownloadStatus.Waiting,
                CreateTime = entry.CreateTime,
                OnCompletedCallback = onCompleted,
                Authorization = authorization
            };

            Application.Current.Dispatcher.Invoke(() => Tasks.Insert(0, task));

            if (reusableEntry != null && TryStartReuseCompletedDownload(task, reusableEntry))
            {
                return task;
            }

            if (!string.Equals(filePath, preferredPath, StringComparison.OrdinalIgnoreCase) &&
                TryStartRemoteValidatedLocalReuse(task, preferredPath, authorization))
            {
                return task;
            }

            QueueRemoteDownload(task, authorization);

            return task;
        }

        private DownloadEntry? FindReusableCompletedEntry(string url, string targetDirectory, string destinationPath, string preferredSourcePath)
        {
            using var db = CreateDbClient();
            var completedEntries = db.Queryable<DownloadEntry>()
                .Where(x => x.Status == (int)DownloadStatus.Completed && x.Url == url)
                .OrderByDescending(x => x.CompleteTime)
                .ToList();

            var preferredEntry = completedEntries.FirstOrDefault(entry =>
                string.Equals(entry.SavePath, preferredSourcePath, StringComparison.OrdinalIgnoreCase) &&
                CanReuseCompletedEntry(entry, targetDirectory, destinationPath));

            return preferredEntry ?? completedEntries.FirstOrDefault(entry =>
                CanReuseCompletedEntry(entry, targetDirectory, destinationPath));
        }

        private bool TryStartReuseCompletedDownload(DownloadTask task, DownloadEntry sourceEntry)
        {
            string targetDirectory = Path.GetDirectoryName(task.SavePath) ?? Config.DefaultDownloadPath;
            if (!CanReuseCompletedEntry(sourceEntry, targetDirectory, task.SavePath))
                return false;

            task.LocalReuseSourcePath = sourceEntry.SavePath;
            task.LocalReuseRequiresRemoteValidation = false;
            long expectedBytes = GetReusableSourceLength(sourceEntry);
            _ = StartLocalReuseCopyAsync(task, sourceEntry.SavePath, expectedBytes, task.Authorization, fallbackToRemoteOnFailure: true);

            return true;
        }

        private bool TryStartRemoteValidatedLocalReuse(DownloadTask task, string sourcePath, string? authorization)
        {
            if (!CanAttemptRemoteValidation(task.Url))
                return false;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) || File.Exists(sourcePath + ".aria2"))
                return false;

            if (string.Equals(sourcePath, task.SavePath, StringComparison.OrdinalIgnoreCase))
                return false;

            long expectedBytes;
            try
            {
                expectedBytes = new FileInfo(sourcePath).Length;
            }
            catch
            {
                return false;
            }

            task.LocalReuseSourcePath = sourcePath;
            task.LocalReuseRequiresRemoteValidation = true;
            _ = StartLocalReuseCopyAsync(task, sourcePath, expectedBytes, authorization, fallbackToRemoteOnFailure: true);
            return true;
        }

        private async Task StartLocalReuseCopyAsync(DownloadTask task, string sourcePath, long expectedBytes, string? authorization, bool fallbackToRemoteOnFailure)
        {
            CancellationTokenSource? cancellationTokenSource = null;

            try
            {
                if (IsDisposingOrDisposed)
                    return;

                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                    throw new FileNotFoundException("Local reuse source file not found.", sourcePath);

                cancellationTokenSource = new CancellationTokenSource();
                task.CancellationTokenSource = cancellationTokenSource;
                _localCopyTasks[task.Id] = task;

                if (task.LocalReuseRequiresRemoteValidation)
                {
                    var validationInfo = await TryValidateLocalFileAgainstRemoteAsync(task.Url, sourcePath, authorization, cancellationTokenSource.Token).ConfigureAwait(false);
                    if (validationInfo == null)
                    {
                        log.Info($"Remote validation did not match local duplicate, fallback to remote download. Source: {sourcePath}");
                        task.LocalReuseSourcePath = null;
                        task.LocalReuseRequiresRemoteValidation = false;
                        ResetTaskToWaiting(task);
                        QueueRemoteDownload(task, authorization);
                        return;
                    }

                    expectedBytes = validationInfo.ContentLength ?? expectedBytes;
                    log.Info($"Validated local duplicate against remote. Source: {sourcePath}, ETag: {validationInfo.ETag ?? "-"}, LastModified: {validationInfo.LastModified?.ToString("O") ?? "-"}");
                }

                string targetDirectory = Path.GetDirectoryName(task.SavePath) ?? Config.DefaultDownloadPath;
                Directory.CreateDirectory(targetDirectory);

                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Downloading;
                    task.ProgressValue = 0;
                    task.ErrorMessage = null;
                    task.SpeedText = string.Empty;
                    task.TotalBytes = expectedBytes > 0 ? expectedBytes : 0;
                    task.DownloadedBytes = 0;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Downloading);

                byte[] buffer = ArrayPool<byte>.Shared.Rent(LocalCopyBufferSize);
                try
                {
                    using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, LocalCopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    if (expectedBytes <= 0)
                        expectedBytes = sourceStream.Length;

                    using var destinationStream = new FileStream(task.SavePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, LocalCopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                    long copiedBytes = 0;
                    long intervalBytes = 0;
                    var intervalStopwatch = Stopwatch.StartNew();
                    var uiStopwatch = Stopwatch.StartNew();

                    while (true)
                    {
                        int bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, LocalCopyBufferSize), cancellationTokenSource.Token).ConfigureAwait(false);
                        if (bytesRead <= 0)
                            break;

                        await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationTokenSource.Token).ConfigureAwait(false);

                        copiedBytes += bytesRead;
                        intervalBytes += bytesRead;

                        if (uiStopwatch.Elapsed >= LocalCopyProgressInterval || (expectedBytes > 0 && copiedBytes >= expectedBytes))
                        {
                            long speed = intervalStopwatch.ElapsedMilliseconds > 0
                                ? intervalBytes * 1000 / intervalStopwatch.ElapsedMilliseconds
                                : 0;

                            long currentCopiedBytes = copiedBytes;
                            long currentTotalBytes = expectedBytes > 0 ? expectedBytes : copiedBytes;
                            int progress = currentTotalBytes > 0 ? (int)Math.Min(100, currentCopiedBytes * 100 / currentTotalBytes) : 0;

                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                task.TotalBytes = currentTotalBytes;
                                task.DownloadedBytes = currentCopiedBytes;
                                task.ProgressValue = progress;
                                task.SpeedText = DownloadTask.FormatSpeed(speed);
                            });

                            intervalBytes = 0;
                            intervalStopwatch.Restart();
                            uiStopwatch.Restart();
                        }
                    }

                    await destinationStream.FlushAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                long completedBytes = new FileInfo(task.SavePath).Length;
                long totalBytes = expectedBytes > 0 ? expectedBytes : completedBytes;

                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.TotalBytes = totalBytes;
                    task.DownloadedBytes = completedBytes;
                    task.ProgressValue = totalBytes > 0 ? 100 : 0;
                    task.Status = DownloadStatus.Completed;
                    task.SpeedText = string.Empty;
                    task.ErrorMessage = null;
                });

                UpdateEntryCompleted(task);
                log.Info($"Reused completed download by streamed local copy. Source: {sourcePath}, Target: {task.SavePath}");
                task.OnCompletedCallback?.Invoke(task);
                DownloadCompleted?.Invoke(this, task);
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(task.SavePath);
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Paused;
                    task.ProgressValue = 0;
                    task.DownloadedBytes = 0;
                    task.SpeedText = string.Empty;
                    task.ErrorMessage = null;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Paused);
                log.Info($"Local reuse copy canceled: {task.FileName}");
            }
            catch (Exception ex)
            {
                log.Warn($"Local reuse failed for {task.FileName}, fallback to remote download: {ex.Message}");
                TryDeleteFile(task.SavePath);

                if (fallbackToRemoteOnFailure)
                {
                    task.LocalReuseSourcePath = null;
                    task.LocalReuseRequiresRemoteValidation = false;
                    ResetTaskToWaiting(task);
                    QueueRemoteDownload(task, authorization);
                }
                else
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        task.Status = DownloadStatus.Failed;
                        task.ErrorMessage = ex.Message;
                        task.SpeedText = string.Empty;
                    });
                    UpdateEntryStatus(task.Id, DownloadStatus.Failed, ex.Message);
                }
            }
            finally
            {
                _localCopyTasks.TryRemove(task.Id, out _);
                if (ReferenceEquals(task.CancellationTokenSource, cancellationTokenSource))
                    task.CancellationTokenSource = null;
                cancellationTokenSource?.Dispose();
            }
        }

        private async Task<RemoteFileValidationInfo?> TryValidateLocalFileAgainstRemoteAsync(string url, string sourcePath, string? authorization, CancellationToken cancellationToken)
        {
            if (!CanAttemptRemoteValidation(url))
                return null;

            if (!File.Exists(sourcePath) || File.Exists(sourcePath + ".aria2"))
                return null;

            long localFileLength = new FileInfo(sourcePath).Length;
            var validationInfo = await TryGetRemoteFileValidationInfoAsync(url, authorization, cancellationToken).ConfigureAwait(false);
            if (validationInfo?.ContentLength is not long remoteLength || remoteLength <= 0)
                return null;

            return remoteLength == localFileLength ? validationInfo : null;
        }

        private async Task<RemoteFileValidationInfo?> TryGetRemoteFileValidationInfoAsync(string url, string? authorization, CancellationToken cancellationToken)
        {
            using var headRequest = CreateRemoteValidationRequest(HttpMethod.Head, url, authorization);
            try
            {
                using var response = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return CreateRemoteFileValidationInfo(response);

                if (response.StatusCode != HttpStatusCode.MethodNotAllowed && response.StatusCode != HttpStatusCode.NotImplemented)
                    return null;
            }
            catch (HttpRequestException)
            {
                return null;
            }

            using var rangeRequest = CreateRemoteValidationRequest(HttpMethod.Get, url, authorization);
            rangeRequest.Headers.Range = new RangeHeaderValue(0, 0);
            using var rangeResponse = await _httpClient.SendAsync(rangeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!rangeResponse.IsSuccessStatusCode && rangeResponse.StatusCode != HttpStatusCode.PartialContent)
                return null;

            return CreateRemoteFileValidationInfo(rangeResponse);
        }

        private static RemoteFileValidationInfo CreateRemoteFileValidationInfo(HttpResponseMessage response)
        {
            return new RemoteFileValidationInfo
            {
                ContentLength = response.Content.Headers.ContentRange?.Length ?? response.Content.Headers.ContentLength,
                ETag = response.Headers.ETag?.Tag,
                LastModified = response.Content.Headers.LastModified
            };
        }

        private static HttpRequestMessage CreateRemoteValidationRequest(HttpMethod method, string url, string? authorization)
        {
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(authorization) && authorization.Contains(':'))
            {
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
            }
            return request;
        }

        private static bool CanAttemptRemoteValidation(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private void QueueRemoteDownload(DownloadTask task, string? authorization)
        {
            _activeTasks.AddOrUpdate(task.Id, task, (key, old) => task);
            _ = StartDownloadAsync(task, authorization);
        }

        private void ResetTaskToWaiting(DownloadTask task)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                task.Status = DownloadStatus.Waiting;
                task.ProgressValue = 0;
                task.ErrorMessage = null;
                task.SpeedText = string.Empty;
                task.TotalBytes = 0;
                task.DownloadedBytes = 0;
            });
            UpdateEntryStatus(task.Id, DownloadStatus.Waiting);
        }

        private static long GetReusableSourceLength(DownloadEntry entry)
        {
            if (entry.TotalBytes > 0)
                return entry.TotalBytes;

            return File.Exists(entry.SavePath) ? new FileInfo(entry.SavePath).Length : 0;
        }

        private static bool CanRetryLocalReuse(DownloadTask task)
        {
            return !string.IsNullOrWhiteSpace(task.LocalReuseSourcePath) && File.Exists(task.LocalReuseSourcePath);
        }

        private static bool CanReuseCompletedEntry(DownloadEntry entry, string targetDirectory, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(entry.SavePath))
                return false;

            if (string.Equals(entry.SavePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                return false;

            string? sourceDirectory = Path.GetDirectoryName(entry.SavePath);
            if (!string.Equals(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
                return false;

            return IsEntryBackedByCompleteFile(entry);
        }

        private static bool IsEntryBackedByCompleteFile(DownloadEntry entry)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entry.SavePath) || !File.Exists(entry.SavePath))
                    return false;

                if (File.Exists(entry.SavePath + ".aria2"))
                    return false;

                long fileLength = new FileInfo(entry.SavePath).Length;
                if (entry.TotalBytes > 0)
                    return entry.DownloadedBytes >= entry.TotalBytes && fileLength == entry.TotalBytes;

                return entry.CompleteTime != null;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private async Task StartDownloadAsync(DownloadTask task, string? authorization = null, bool isRetryAfterCacheClean = false)
        {
            try
            {
                if (IsDisposingOrDisposed)
                    return;

                await EnsureAria2cRunningAsync();

                if (IsDisposingOrDisposed)
                    return;

                StartPolling(); // Wake up polling timer in case it was stopped due to idle

                Application.Current?.Dispatcher.BeginInvoke(() => task.Status = DownloadStatus.Downloading);
                UpdateEntryStatus(task.Id, DownloadStatus.Downloading);

                bool isMagnet = task.Url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);

                // Build options for this download
                string dir = Path.GetDirectoryName(task.SavePath) ?? Config.DefaultDownloadPath;

                var options = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["dir"] = dir,
                };

                // For magnet/BT, don't set "out" as aria2 determines the filename from metadata
                if (!isMagnet)
                {
                    string fileName = Path.GetFileName(task.SavePath);
                    options["out"] = fileName;
                }

                string auth = authorization ?? task.Authorization;
                if (!string.IsNullOrWhiteSpace(auth) && auth.Contains(':'))
                {
                    string[] parts = auth.Split(':', 2);
                    options["http-user"] = parts[0];
                    options["http-passwd"] = parts[1];
                }

                // Call aria2.addUri via JSON-RPC
                var response = await RpcCallAsync("aria2.addUri",
                    new object[] { $"token:{RpcSecret}", new[] { task.Url }, options });

                if (response != null)
                {
                    // Check for RPC-level error (e.g., stale .aria2 cache conflict)
                    var errorObj = response["error"];
                    if (errorObj != null)
                    {
                        string? errorMsg = errorObj["message"]?.ToString();
                        if (!isRetryAfterCacheClean && TryClearStaleCacheFiles(task.SavePath))
                        {
                            log.Info($"Cleared stale .aria2 cache for {task.FileName}, retrying download");
                            await StartDownloadAsync(task, authorization, isRetryAfterCacheClean: true);
                            return;
                        }
                        throw new Exception(errorMsg ?? "aria2c RPC error");
                    }

                    task.Status = DownloadStatus.Downloading;
                    string? gid = response["result"]?.ToString();
                    task.Gid = gid;
                    _activeTasks[task.Id] = task;
                    log.Info($"Download started via RPC, GID: {gid}, File: {task.FileName}");
                }
                else
                {
                    throw new Exception("No response from aria2c RPC");
                }
            }
            catch (Exception ex)
            {
                if (IsDisposingOrDisposed)
                {
                    log.Debug($"Download aborted during shutdown: {ex.Message}");
                    return;
                }

                log.Error($"Download failed: {ex.Message}", ex);
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Failed;
                    task.ErrorMessage = ex.Message;
                    task.SpeedText = string.Empty;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Failed, ex.Message);
                task.OnCompletedCallback?.Invoke(task);
                DownloadCompleted?.Invoke(this, task);
            }
        }

        public void CancelDownload(DownloadTask task)
        {
            if (task.CancellationTokenSource != null)
            {
                task.CancellationTokenSource.Cancel();
                return;
            }

            if (!string.IsNullOrEmpty(task.Gid))
            {
                TryRemoveGidAsync(task.Gid);
            }
            _activeTasks.TryRemove(task.Id, out _);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                task.Status = DownloadStatus.Paused;
                task.SpeedText = string.Empty;
            });
            UpdateEntryStatus(task.Id, DownloadStatus.Paused);
            _activeTasks.TryRemove(task.Id, out _);
        }

        public void PauseDownload(DownloadTask task)
        {
            if (task.CancellationTokenSource != null)
            {
                task.CancellationTokenSource.Cancel();
                return;
            }

            if (!string.IsNullOrEmpty(task.Gid))
            {
                Task.Run(async () =>
                {
                    try { await RpcCallAsync("aria2.pause", new object[] { $"token:{RpcSecret}", task.Gid }); }
                    catch (Exception ex) { log.Debug($"RPC pause failed for GID {task.Gid}: {ex.Message}"); }
                });
            }
            _activeTasks.TryRemove(task.Id, out _);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                task.Status = DownloadStatus.Paused;
                task.SpeedText = string.Empty;
            });
            UpdateEntryStatus(task.Id, DownloadStatus.Paused);
        }

        public void ResumeDownload(DownloadTask task)
        {
            if (CanRetryLocalReuse(task))
            {
                _ = StartLocalReuseCopyAsync(task, task.LocalReuseSourcePath!, task.TotalBytes, task.Authorization, fallbackToRemoteOnFailure: true);
                return;
            }

            if (!string.IsNullOrEmpty(task.Gid))
            {
                _activeTasks.AddOrUpdate(task.Id, task, (key, old) => task);
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Downloading;
                    task.SpeedText = string.Empty;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Downloading);
                Task.Run(async () =>
                {
                    try
                    {
                        await EnsureAria2cRunningAsync();
                        StartPolling();
                        await RpcCallAsync("aria2.unpause", new object[] { $"token:{RpcSecret}", task.Gid });
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"RPC unpause failed for GID {task.Gid}: {ex.Message}");
                        // Fall back to retry
                        Application.Current?.Dispatcher.BeginInvoke(() => RetryDownload(task));
                    }
                });
            }
            else
            {
                RetryDownload(task);
            }
        }

        public void RetryDownload(DownloadTask task)
        {
            task.Status = DownloadStatus.Waiting;
            task.ProgressValue = 0;
            task.ErrorMessage = null;
            task.SpeedText = string.Empty;
            task.Gid = null;

            string directory = Path.GetDirectoryName(task.SavePath) ?? Config.DefaultDownloadPath;
            string fullPath = Path.Combine(directory, task.FileName);
            string rf = fullPath + ".aria2";
            if (File.Exists(rf))
            {
                File.Delete(rf);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

            }


            UpdateEntryStatus(task.Id, DownloadStatus.Waiting);

            if (CanRetryLocalReuse(task))
            {
                _ = StartLocalReuseCopyAsync(task, task.LocalReuseSourcePath!, task.TotalBytes, task.Authorization, fallbackToRemoteOnFailure: true);
                return;
            }

            QueueRemoteDownload(task, task.Authorization);
        }

        public void DeleteRecord(int id)
        {
            using var db = CreateDbClient();
            db.Deleteable<DownloadEntry>().In(id).ExecuteCommand();
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                if (task.CancellationTokenSource != null)
                    task.CancellationTokenSource.Cancel();
                if (!string.IsNullOrEmpty(task.Gid))
                    TryRemoveGidAsync(task.Gid);
                _activeTasks.TryRemove(task.Id, out _);
                _localCopyTasks.TryRemove(task.Id, out _);
                Application.Current.Dispatcher.Invoke(() => Tasks.Remove(task));
            }
        }

        public void DeleteRecords(int[] ids, bool deleteFiles = false)
        {
            // Collect file paths before removing from DB
            List<string> filePaths = new();
            if (deleteFiles)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var task in Tasks.Where(t => ids.Contains(t.Id)))
                    {
                        if (!string.IsNullOrEmpty(task.SavePath))
                            filePaths.Add(task.SavePath);
                    }
                });
            }

            using var db = CreateDbClient();
            db.Deleteable<DownloadEntry>().In(ids).ExecuteCommand();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var toRemove = Tasks.Where(t => ids.Contains(t.Id)).ToList();
                foreach (var task in toRemove)
                {
                    if (task.CancellationTokenSource != null)
                        task.CancellationTokenSource.Cancel();
                    if (!string.IsNullOrEmpty(task.Gid))
                        TryRemoveGidAsync(task.Gid);
                    _activeTasks.TryRemove(task.Id, out _);
                    _localCopyTasks.TryRemove(task.Id, out _);
                    Tasks.Remove(task);
                }
            });

            // Delete files after DB/UI cleanup
            if (deleteFiles)
            {
                foreach (var path in filePaths)
                {
                    try
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"Failed to delete file {path}: {ex.Message}");
                    }
                }
            }
        }

        public void ClearAllRecords()
        {
            using var db = CreateDbClient();
            db.Deleteable<DownloadEntry>().ExecuteCommand();

            foreach (var task in _localCopyTasks.Values)
            {
                task.CancellationTokenSource?.Cancel();
            }

            foreach (var task in _activeTasks.Values)
            {
                if (!string.IsNullOrEmpty(task.Gid))
                    TryRemoveGidAsync(task.Gid);
            }
            _activeTasks.Clear();
            _localCopyTasks.Clear();
            Application.Current.Dispatcher.Invoke(() => Tasks.Clear());
        }

        /// <summary>
        /// Best-effort removal of a download from aria2c via RPC
        /// </summary>
        private void TryRemoveGidAsync(string gid)
        {
            Task.Run(async () =>
            {
                try { await RpcCallAsync("aria2.remove", new object[] { $"token:{RpcSecret}", gid }); }
                catch (Exception ex) { log.Debug($"RPC remove failed for GID {gid}: {ex.Message}"); }
            });
        }

        public void LoadRecords(string? searchKeyword = null, int pageSize = 20, int page = 1)
        {
            using var db = CreateDbClient();
            var query = db.Queryable<DownloadEntry>();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                query = query.Where(x => x.FileName.Contains(searchKeyword) || x.Url.Contains(searchKeyword));
            }

            var entries = query.OrderByDescending(x => x.CreateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Tasks.Clear();
                foreach (var entry in entries)
                {
                    var status = (DownloadStatus)entry.Status;
                    if (status == DownloadStatus.Completed && !File.Exists(entry.SavePath))
                    {
                        status = DownloadStatus.FileDeleted;
                        UpdateEntryStatus(entry.Id, DownloadStatus.FileDeleted);
                    }

                    // �������޸���������������ڻ�Ծ�ֵ��У�ֱ�����ø�ʵ�������� UI �� DataBinding ���ò��Ͽ�
                    if (_activeTasks.TryGetValue(entry.Id, out var activeTask))
                    {
                        Tasks.Add(activeTask);
                    }
                    else if (_localCopyTasks.TryGetValue(entry.Id, out var localCopyTask))
                    {
                        Tasks.Add(localCopyTask);
                    }
                    else
                    {
                        Tasks.Add(new DownloadTask
                        {
                            Id = entry.Id,
                            Url = entry.Url,
                            FileName = entry.FileName,
                            SavePath = entry.SavePath,
                            Status = status,
                            TotalBytes = entry.TotalBytes,
                            DownloadedBytes = entry.DownloadedBytes,
                            ProgressValue = entry.TotalBytes > 0 ? (int)(entry.DownloadedBytes * 100 / entry.TotalBytes) : 0,
                            CreateTime = entry.CreateTime,
                            ErrorMessage = entry.ErrorMessage,
                            Authorization = DecodeAuth(entry.Authorization)
                        });
                    }
                }
            });
        }

        public int GetTotalCount(string? searchKeyword = null)
        {
            using var db = CreateDbClient();
            var query = db.Queryable<DownloadEntry>();
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                query = query.Where(x => x.FileName.Contains(searchKeyword) || x.Url.Contains(searchKeyword));
            }
            return query.Count();
        }

        private void UpdateEntryStatus(int id, DownloadStatus status, string? errorMessage = null)
        {
            using var db = CreateDbClient();
            db.Updateable<DownloadEntry>()
                .SetColumns(x => x.Status == (int)status)
                .SetColumns(x => x.ErrorMessage == errorMessage)
                .Where(x => x.Id == id)
                .ExecuteCommand();
        }

        private void UpdateEntryFileName(int id, string fileName)
        {
            using var db = CreateDbClient();
            db.Updateable<DownloadEntry>()
                .SetColumns(x => x.FileName == fileName)
                .Where(x => x.Id == id)
                .ExecuteCommand();
        }

        private void UpdateEntryCompleted(DownloadTask task)
        {
            using var db = CreateDbClient();
            db.Updateable<DownloadEntry>()
                .SetColumns(x => x.Status == (int)DownloadStatus.Completed)
                .SetColumns(x => x.TotalBytes == task.TotalBytes)
                .SetColumns(x => x.DownloadedBytes == task.DownloadedBytes)
                .SetColumns(x => x.CompleteTime == DateTime.Now)
                .Where(x => x.Id == task.Id)
                .ExecuteCommand();
        }

        /// <summary>
        /// Try to clear stale .aria2 cache files for a given save path.
        /// Returns true if cache files were found and deleted (warranting a retry).
        /// </summary>
        private bool TryClearStaleCacheFiles(string savePath)
        {
            try
            {
                string aria2CacheFile = savePath + ".aria2";
                bool cleared = false;

                if (File.Exists(aria2CacheFile))
                {
                    File.Delete(aria2CacheFile);
                    log.Info($"Deleted stale .aria2 cache: {aria2CacheFile}");
                    cleared = true;
                }

                // Also remove the partial download file if it exists (it's from a stale session)
                if (cleared && File.Exists(savePath))
                {
                    File.Delete(savePath);
                    log.Info($"Deleted stale partial file: {savePath}");
                }

                return cleared;
            }
            catch (Exception ex)
            {
                log.Debug($"Failed to clear stale cache for {savePath}: {ex.Message}");
                return false;
            }
        }

        private static string GetFileNameFromUrl(string url)
        {
            // Handle magnet links: extract display name from dn= parameter
            if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    int dnIndex = url.IndexOf("dn=", StringComparison.OrdinalIgnoreCase);
                    if (dnIndex >= 0)
                    {
                        string dn = url.Substring(dnIndex + 3);
                        int endIndex = dn.IndexOf('&');
                        if (endIndex >= 0) dn = dn.Substring(0, endIndex);
                        dn = Uri.UnescapeDataString(dn).Trim();
                        if (!string.IsNullOrWhiteSpace(dn)) return dn;
                    }
                }
                catch { }
                return $"magnet_{DateTime.Now:yyyyMMddHHmmss}";
            }

            try
            {
                var uri = new Uri(url);
                string fileName = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(fileName) && fileName != "/")
                    return fileName;
            }
            catch { }
            return $"download_{DateTime.Now:yyyyMMddHHmmss}";
        }

        /// <summary>
        /// Generate a unique file path by appending (1), (2), etc. if the file or its .aria2 cache already exists.
        /// </summary>
        private static string GetUniqueFilePath(string directory, string fileName)
        {
            string filePath = Path.Combine(directory, fileName);
            if (!File.Exists(filePath) && !File.Exists(filePath + ".aria2"))
                return filePath;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);

            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(directory, $"{nameWithoutExt}({i}){ext}");
                if (!File.Exists(candidate) && !File.Exists(candidate + ".aria2"))
                    return candidate;
            }
            // Fallback: use timestamp
            return Path.Combine(directory, $"{nameWithoutExt}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
        }

        /// <summary>
        /// Encode authorization for storage (Base64 to avoid plain text in DB)
        /// </summary>
        private static string? EncodeAuth(string? auth)
        {
            if (string.IsNullOrEmpty(auth)) return null;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(auth));
        }

        /// <summary>
        /// Decode authorization from storage
        /// </summary>
        private static string? DecodeAuth(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
            catch { return encoded; }
        }
    }
}
