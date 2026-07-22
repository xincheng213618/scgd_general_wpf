#pragma warning disable CA1822,CA1863
using log4net;
using Newtonsoft.Json.Linq;
using SqlSugar;
using ColorVision.Update;
using ColorVision.UI;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Desktop.Download
{

    public class Aria2cDownloadManager : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(Aria2cDownloadManager));
        private static readonly string[] TellStatusKeys = { "status", "totalLength", "completedLength", "downloadSpeed", "errorCode", "errorMessage", "bittorrent", "files" };
        private const int PollIntervalMs = 300;

        private static Aria2cDownloadManager? _instance;
        private static readonly object _locker = new();

        public static Aria2cDownloadManager GetInstance()
        {
            lock (_locker)
            {
                if (_instance == null)
                {
                    var application = Application.Current;
                    if (application?.Dispatcher != null && !application.Dispatcher.CheckAccess())
                    {
                        _instance = application.Dispatcher.InvokeAsync(() => new Aria2cDownloadManager()).Task.GetAwaiter().GetResult();
                    }
                    else
                    {
                        _instance = new Aria2cDownloadManager();
                    }
                }

                return _instance;
            }
        }

        public static string DirectoryPath { get; set; } = Environments.DirDownloads;
        public static string DbPath { get; set; } = Path.Combine(DirectoryPath, "Downloads.db");

        public ObservableCollection<DownloadTask> Tasks { get; } = new();
        private readonly ConcurrentDictionary<int, DownloadTask> _activeTasks = new();
        private readonly ConcurrentDictionary<int, DownloadTask> _localCopyTasks = new();
        private readonly DownloadTaskStore _store;
        private readonly Aria2Daemon _daemon;
        private readonly Aria2RpcClient _rpcClient;
        private readonly DownloadReuseService _reuseService;
        private readonly SemaphoreSlim _daemonLifecycleLock = new(1, 1);

        private int _rpcPort = 6800;
        private const string RpcSecret = "ColorVisionDL";
        private Timer? _pollTimer;
        private int _disposeState;
        private int _isPollCallback;

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

        private void RunFireAndForget(Func<Task> operation, string failureMessage)
        {
            _ = RunFireAndForgetCoreAsync(operation, failureMessage);
        }

        private async Task RunFireAndForgetCoreAsync(Func<Task> operation, string failureMessage)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                log.Debug($"{failureMessage}: {ex.Message}");
            }
            catch (Exception ex)
            {
                log.Error(failureMessage, ex);
            }
        }

        private static void PostToDispatcher(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.InvokeAsync(() => RunDispatcherAction(action));
        }

        private static void RunOnDispatcher(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.InvokeAsync(action).Task.GetAwaiter().GetResult();
        }

        private static void RunDispatcherAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                log.Error("Dispatcher action failed.", ex);
            }
        }

        private Aria2cDownloadManager()
        {
            Directory.CreateDirectory(DirectoryPath);
            _store = new DownloadTaskStore(DbPath);
            _store.Initialize();

            // Use configured port
            _rpcPort = Config.RpcPort;
            _daemon = new Aria2Daemon();
            _rpcClient = new Aria2RpcClient(() => _rpcPort, RpcSecret);
            _reuseService = new DownloadReuseService();

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
            _daemon.Dispose();
            _rpcClient.Dispose();
            _reuseService.Dispose();
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
            RunFireAndForget(async () =>
            {
                if (IsDisposingOrDisposed) return;
                if (!IsAria2cRunning) return;
                string limit = Config.EnableSpeedLimit ? $"{Config.SpeedLimitMB}M" : "0";
                var options = new Dictionary<string, string> { ["max-overall-download-limit"] = limit };
                await _rpcClient.CallAsync("aria2.changeGlobalOption", options).ConfigureAwait(false);
                PostToDispatcher(() => StatusMessage = Properties.Resources.ConfigApplied);
                log.Info($"Speed limit applied: {limit}");
            }, "Failed to apply speed limit.");
        }

        private void ApplyMaxConcurrentTasksAsync()
        {
            RunFireAndForget(async () =>
            {
                if (IsDisposingOrDisposed) return;
                if (!IsAria2cRunning) return;
                var options = new Dictionary<string, string> { ["max-concurrent-downloads"] = Config.MaxConcurrentTasks.ToString() };
                await _rpcClient.CallAsync("aria2.changeGlobalOption", options).ConfigureAwait(false);
                PostToDispatcher(() => StatusMessage = Properties.Resources.ConfigApplied);
                log.Info($"Max concurrent tasks applied: {Config.MaxConcurrentTasks}");
            }, "Failed to apply max concurrent tasks.");
        }

        /// <summary>
        /// Whether we have a working connection to an aria2c daemon (own process or reused)
        /// </summary>
        public bool IsAria2cRunning
        {
            get
            {
                return _daemon.IsRunning;
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

            RunFireAndForget(async () =>
            {
                if (IsDisposingOrDisposed) return;
                await EnsureAria2cRunningAsync().ConfigureAwait(false);
            }, "Preload aria2c failed.");
        }

        public static SqlSugarClient CreateDbClient()
        {
            return DownloadTaskStore.CreateDbClient(DbPath);
        }

        #region aria2c RPC Daemon

        private async Task EnsureAria2cRunningAsync()
        {
            if (IsDisposingOrDisposed)
                return;

            bool disableSystemProxy = UpdateNetworkConfig.Instance.DisableSystemProxyForUpdates;
            if (_daemon.IsRunningForNetworkMode(disableSystemProxy))
                return;

            await _daemonLifecycleLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsDisposingOrDisposed)
                    return;

                disableSystemProxy = UpdateNetworkConfig.Instance.DisableSystemProxyForUpdates;
                if (_daemon.IsRunningForNetworkMode(disableSystemProxy))
                    return;

                if (_daemon.IsRunning)
                    await Task.Run(StopAria2cDaemon).ConfigureAwait(false);

                await StartAria2cDaemonAsync(disableSystemProxy).ConfigureAwait(false);
            }
            finally
            {
                _daemonLifecycleLock.Release();
            }
        }

        private async Task StartAria2cDaemonAsync(bool disableSystemProxy)
        {
            if (IsDisposingOrDisposed)
                return;

            if (_daemon.IsRunning)
                return;

            int requestedPort = _rpcPort;
            int preparedPort = _daemon.PreparePort(_rpcPort);
            if (preparedPort != _rpcPort)
            {
                log.Warn($"Port {_rpcPort} is in use, switching to port {preparedPort}");
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    StatusMessage = string.Format(Properties.Resources.PortSwitched, requestedPort, preparedPort);
                });
                _rpcPort = preparedPort;
            }

            _daemon.Start(_rpcPort, RpcSecret, Config, disableSystemProxy);

            // Wait for RPC to be ready
            for (int i = 0; i < 30; i++)
            {
                if (IsDisposingOrDisposed)
                    return;

                await Task.Delay(100).ConfigureAwait(false);
                try
                {
                    var response = await _rpcClient.CallAsync("aria2.getVersion").ConfigureAwait(false);
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

        private void StopAria2cDaemon()
        {
            StopPolling();

            _daemon.Stop(() =>
            {
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                _rpcClient.CallAsync("aria2.forceShutdown", cancellationTokenSource.Token)
                    .GetAwaiter()
                    .GetResult();
            });

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

        private void PollCallback(object? state)
        {
            _ = PollAsync();
        }

        private async Task PollAsync()
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
                    var globalStat = await _rpcClient.CallAsync("aria2.getGlobalStat");
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
                        var status = await _rpcClient.CallAsync("aria2.tellStatus", task.Gid, TellStatusKeys);

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

                        if (rpcStatus != "complete")
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                task.ProgressValue = progress;
                                task.TotalBytes = totalLength;
                                task.DownloadedBytes = completedLength;
                                task.SpeedText = speedText;
                            });
                        }

                        if (rpcStatus == "complete")
                        {
                            _activeTasks.TryRemove(task.Id, out _);

                            if (TryGetCompletedFileMetrics(task, totalLength, completedLength, out long finalTotalBytes, out long finalDownloadedBytes, out string? completeError))
                            {
                                CompleteTask(task, finalTotalBytes, finalDownloadedBytes);
                            }
                            else
                            {
                                FailTask(task, completeError);
                                log.Error($"Download completed with invalid file: {task.SavePath}. {completeError}");
                            }
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
                                _activeTasks.TryRemove(task.Id, out _);
                                FailTask(task, errorMsg ?? "Unknown error");
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

        private static long ParseLong(JToken? token)
        {
            return long.TryParse(token?.ToString(), out long value) ? value : 0;
        }

        #endregion

        public void AutoRestartIncompleteDownloadsAsync()
        {
            if (IsDisposingOrDisposed)
                return;

            RunFireAndForget(() => Task.Run(AutoRestartIncompleteDownloads), "Auto-restart incomplete downloads failed.");
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

                var incompleteEntries = _store.GetIncompleteEntries();

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

            fileName ??= DownloadPathResolver.GetFileNameFromUrl(url);
            string preferredPath = Path.Combine(targetDir, fileName);
            string filePath = DownloadPathResolver.GetUniqueFilePath(targetDir, fileName);
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

            entry.Id = _store.Insert(entry);

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

            PostToDispatcher(() => Tasks.Insert(0, task));

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
            var completedEntries = _store.GetCompletedEntriesByUrl(url);

            var preferredEntry = completedEntries.FirstOrDefault(entry =>
                string.Equals(entry.SavePath, preferredSourcePath, StringComparison.OrdinalIgnoreCase) &&
                DownloadReuseService.CanReuseCompletedEntry(entry, targetDirectory, destinationPath));

            return preferredEntry ?? completedEntries.FirstOrDefault(entry =>
                DownloadReuseService.CanReuseCompletedEntry(entry, targetDirectory, destinationPath));
        }

        private bool TryStartReuseCompletedDownload(DownloadTask task, DownloadEntry sourceEntry)
        {
            string targetDirectory = Path.GetDirectoryName(task.SavePath) ?? Config.DefaultDownloadPath;
            if (!DownloadReuseService.CanReuseCompletedEntry(sourceEntry, targetDirectory, task.SavePath))
                return false;

            task.LocalReuseSourcePath = sourceEntry.SavePath;
            task.LocalReuseRequiresRemoteValidation = false;
            long expectedBytes = DownloadReuseService.GetReusableSourceLength(sourceEntry);
            _ = StartLocalReuseCopyAsync(task, sourceEntry.SavePath, expectedBytes, task.Authorization, fallbackToRemoteOnFailure: true);

            return true;
        }

        private bool TryStartRemoteValidatedLocalReuse(DownloadTask task, string sourcePath, string? authorization)
        {
            if (!DownloadReuseService.TryGetRemoteValidatedCandidate(task.Url, sourcePath, task.SavePath, out long expectedBytes))
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
                    var validationInfo = await _reuseService.TryValidateLocalFileAgainstRemoteAsync(task.Url, sourcePath, authorization, cancellationTokenSource.Token).ConfigureAwait(false);
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

                var copyResult = await LocalFileCopyService.CopyAsync(
                    sourcePath,
                    task.SavePath,
                    expectedBytes,
                    progress =>
                    {
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            task.TotalBytes = progress.TotalBytes;
                            task.DownloadedBytes = progress.CopiedBytes;
                            task.ProgressValue = progress.Progress;
                            task.SpeedText = DownloadTask.FormatSpeed(progress.BytesPerSecond);
                        });
                    },
                    cancellationTokenSource.Token).ConfigureAwait(false);

                CompleteTask(task, copyResult.TotalBytes, copyResult.CompletedBytes);
                log.Info($"Reused completed download by streamed local copy. Source: {sourcePath}, Target: {task.SavePath}");
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

        private static bool TryGetCompletedFileMetrics(DownloadTask task, long rpcTotalLength, long rpcCompletedLength, out long totalBytes, out long downloadedBytes, out string? errorMessage)
        {
            totalBytes = rpcTotalLength;
            downloadedBytes = rpcCompletedLength;
            errorMessage = null;

            bool isMagnet = task.Url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);
            if (!isMagnet)
            {
                if (string.IsNullOrWhiteSpace(task.SavePath) || !File.Exists(task.SavePath))
                {
                    errorMessage = "Downloaded file was not found.";
                    return false;
                }

                if (File.Exists(task.SavePath + ".aria2"))
                {
                    errorMessage = "Downloaded file still has an aria2 cache file.";
                    return false;
                }

                long fileLength;
                try
                {
                    fileLength = new FileInfo(task.SavePath).Length;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    return false;
                }

                if (fileLength <= 0)
                {
                    errorMessage = "Downloaded file is empty.";
                    return false;
                }

                downloadedBytes = fileLength;
            }

            if (downloadedBytes <= 0 && rpcCompletedLength > 0)
                downloadedBytes = rpcCompletedLength;

            if (totalBytes <= 0)
                totalBytes = downloadedBytes;

            if (downloadedBytes > totalBytes)
                totalBytes = downloadedBytes;

            if (totalBytes > 0 && downloadedBytes < totalBytes)
            {
                errorMessage = $"Downloaded file is incomplete: {downloadedBytes}/{totalBytes} bytes.";
                return false;
            }

            return true;
        }

        private void CompleteTask(DownloadTask task, long totalBytes, long downloadedBytes)
        {
            ApplyTaskUpdate(() =>
            {
                task.TotalBytes = totalBytes;
                task.DownloadedBytes = downloadedBytes;
                task.ProgressValue = totalBytes > 0 ? 100 : 0;
                task.Status = DownloadStatus.Completed;
                task.SpeedText = string.Empty;
                task.ErrorMessage = null;
            });

            UpdateEntryCompleted(task);
            task.OnCompletedCallback?.Invoke(task);
            DownloadCompleted?.Invoke(this, task);
        }

        private void FailTask(DownloadTask task, string? errorMessage)
        {
            ApplyTaskUpdate(() =>
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = errorMessage ?? "Unknown error";
                task.SpeedText = string.Empty;
            });

            UpdateEntryStatus(task.Id, DownloadStatus.Failed, errorMessage);
            task.OnCompletedCallback?.Invoke(task);
            DownloadCompleted?.Invoke(this, task);
        }

        private static void ApplyTaskUpdate(Action update)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(update).Task.GetAwaiter().GetResult();
                return;
            }

            update();
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
                var response = await _rpcClient.CallAsync("aria2.addUri", new[] { task.Url }, options);

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
                _activeTasks.TryRemove(task.Id, out _);
                FailTask(task, ex.Message);
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
                RunFireAndForget(async () =>
                {
                    try { await _rpcClient.CallAsync("aria2.pause", task.Gid); }
                    catch (Exception ex) { log.Debug($"RPC pause failed for GID {task.Gid}: {ex.Message}"); }
                }, $"RPC pause task failed for GID {task.Gid}.");
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
            if (DownloadReuseService.CanRetryLocalReuse(task))
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
                RunFireAndForget(async () =>
                {
                    try
                    {
                        await EnsureAria2cRunningAsync();
                        StartPolling();
                        await _rpcClient.CallAsync("aria2.unpause", task.Gid);
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"RPC unpause failed for GID {task.Gid}: {ex.Message}");
                        // Fall back to retry
                        Application.Current?.Dispatcher.BeginInvoke(() => RetryDownload(task));
                    }
                }, $"RPC unpause task failed for GID {task.Gid}.");
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

            if (DownloadReuseService.CanRetryLocalReuse(task))
            {
                _ = StartLocalReuseCopyAsync(task, task.LocalReuseSourcePath!, task.TotalBytes, task.Authorization, fallbackToRemoteOnFailure: true);
                return;
            }

            QueueRemoteDownload(task, task.Authorization);
        }

        public void DeleteRecord(int id)
        {
            _store.Delete(id);
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                if (task.CancellationTokenSource != null)
                    task.CancellationTokenSource.Cancel();
                if (!string.IsNullOrEmpty(task.Gid))
                    TryRemoveGidAsync(task.Gid);
                _activeTasks.TryRemove(task.Id, out _);
                _localCopyTasks.TryRemove(task.Id, out _);
                RunOnDispatcher(() => Tasks.Remove(task));
            }
        }

        public void DeleteRecords(int[] ids, bool deleteFiles = false)
        {
            // Collect file paths before removing from DB
            List<string> filePaths = new();
            if (deleteFiles)
            {
                RunOnDispatcher(() =>
                {
                    foreach (var task in Tasks.Where(t => ids.Contains(t.Id)))
                    {
                        if (!string.IsNullOrEmpty(task.SavePath))
                            filePaths.Add(task.SavePath);
                    }
                });
            }

            _store.DeleteMany(ids);

            RunOnDispatcher(() =>
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
            _store.Clear();

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
            RunOnDispatcher(() => Tasks.Clear());
        }

        /// <summary>
        /// Best-effort removal of a download from aria2c via RPC
        /// </summary>
        private void TryRemoveGidAsync(string gid)
        {
            RunFireAndForget(async () =>
            {
                try { await _rpcClient.CallAsync("aria2.remove", gid); }
                catch (Exception ex) { log.Debug($"RPC remove failed for GID {gid}: {ex.Message}"); }
            }, $"RPC remove task failed for GID {gid}.");
        }

        public void LoadRecords(string? searchKeyword = null, int pageSize = 20, int page = 1)
        {
            var entries = _store.LoadRecords(searchKeyword, pageSize, page);

            RunOnDispatcher(() =>
            {
                Tasks.Clear();
                foreach (var entry in entries)
                {
                    var status = (DownloadStatus)entry.Status;
                    long totalBytes = entry.TotalBytes;
                    long downloadedBytes = entry.DownloadedBytes;
                    string? errorMessage = entry.ErrorMessage;

                    if (status == DownloadStatus.Completed && TryGetUsableCompletedFileLength(entry.SavePath, out long fileLength))
                    {
                        if (totalBytes <= 0 || downloadedBytes <= 0)
                        {
                            totalBytes = fileLength;
                            downloadedBytes = fileLength;
                            UpdateEntryBytes(entry.Id, totalBytes, downloadedBytes);
                        }
                    }
                    else if (status == DownloadStatus.Completed && !File.Exists(entry.SavePath))
                    {
                        status = DownloadStatus.FileDeleted;
                        UpdateEntryStatus(entry.Id, DownloadStatus.FileDeleted);
                    }
                    else if (status == DownloadStatus.Completed)
                    {
                        status = DownloadStatus.Failed;
                        errorMessage = "Downloaded file is empty or incomplete.";
                        UpdateEntryStatus(entry.Id, DownloadStatus.Failed, errorMessage);
                    }

                    // Reuse live task instances so UI bindings stay connected while paging or refreshing.
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
                            TotalBytes = totalBytes,
                            DownloadedBytes = downloadedBytes,
                            ProgressValue = totalBytes > 0 ? (int)(downloadedBytes * 100 / totalBytes) : 0,
                            CreateTime = entry.CreateTime,
                            ErrorMessage = errorMessage,
                            Authorization = DecodeAuth(entry.Authorization)
                        });
                    }
                }
            });
        }

        public int GetTotalCount(string? searchKeyword = null)
        {
            return _store.GetTotalCount(searchKeyword);
        }

        private void UpdateEntryStatus(int id, DownloadStatus status, string? errorMessage = null)
        {
            _store.UpdateStatus(id, status, errorMessage);
        }

        private void UpdateEntryBytes(int id, long totalBytes, long downloadedBytes)
        {
            _store.UpdateBytes(id, totalBytes, downloadedBytes);
        }

        private void UpdateEntryFileName(int id, string fileName)
        {
            _store.UpdateFileName(id, fileName);
        }

        private void UpdateEntryCompleted(DownloadTask task)
        {
            _store.MarkCompleted(task.Id, task.TotalBytes, task.DownloadedBytes, DateTime.Now);
        }

        private static bool TryGetUsableCompletedFileLength(string? filePath, out long fileLength)
        {
            fileLength = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || File.Exists(filePath + ".aria2"))
                    return false;

                fileLength = new FileInfo(filePath).Length;
                return fileLength > 0;
            }
            catch
            {
                fileLength = 0;
                return false;
            }
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
