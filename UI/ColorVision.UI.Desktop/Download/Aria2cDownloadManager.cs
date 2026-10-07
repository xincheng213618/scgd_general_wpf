#pragma warning disable CA1822,CA1863
using ColorVision.Update;
using log4net;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;

namespace ColorVision.UI.Desktop.Download
{
    public class Aria2cDownloadManager : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(Aria2cDownloadManager));
        private static readonly string[] TellStatusKeys = { "status", "totalLength", "completedLength", "downloadSpeed", "errorMessage", "bittorrent", "files", "followedBy" };
        private static readonly object _locker = new();
        private static Aria2cDownloadManager? _instance;
        public static string DirectoryPath { get; set; } = Environments.DirDownloads;
        public static string DbPath { get; set; } = Path.Combine(DirectoryPath, "Downloads.db");

        public static Aria2cDownloadManager GetInstance()
        {
            // Never hold a lock while waiting for the dispatcher that may also request this singleton.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                return dispatcher.InvokeAsync(GetInstance).Task.GetAwaiter().GetResult();
            lock (_locker) return _instance ??= new Aria2cDownloadManager();
        }

        public static Task StopExistingDaemonForUpdateHandoffAsync() => Volatile.Read(ref _instance)?.StopDaemonForUpdateHandoffAsync() ?? Task.CompletedTask;
        public static SqlSugarClient CreateDbClient() => DownloadTaskStore.CreateDbClient(DbPath);

        public static bool IsPathProtectedFromCleanup(string filePath)
        {
            try
            {
                if (File.Exists(filePath + ".aria2")) return true;
                string path = Path.GetFullPath(filePath);
                var instance = Volatile.Read(ref _instance);
                if (instance != null && instance._knownTasks.Values.Any(task => task.Status is not (DownloadStatus.Completed or DownloadStatus.FileDeleted) &&
                    Path.GetFullPath(task.SavePath).Equals(path, StringComparison.OrdinalIgnoreCase))) return true;
                return DownloadTaskStore.IsPathProtectedFromCleanup(DbPath, path);
            }
            catch { return true; }
        }

        public ObservableCollection<DownloadTask> Tasks { get; } = new();
        private readonly ConcurrentDictionary<int, DownloadTask> _knownTasks = new();
        private readonly ConcurrentDictionary<int, DownloadTask> _activeTasks = new();
        private readonly ConcurrentDictionary<int, DownloadTask> _localCopyTasks = new();
        private readonly DownloadTaskStore _store;
        private readonly IAria2Daemon _daemon;
        private readonly IAria2RpcClient _rpcClient;
        private readonly DownloadReuseService _reuseService;
        private readonly DownloadManagerConfig _config;
        private readonly DownloadCopyGate _copyGate = new();
        private readonly SemaphoreSlim _daemonLifecycleLock = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly object _pathLock = new();
        private readonly HashSet<string> _reservedPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _pollLock = new();
        private readonly bool _enablePolling;
        private readonly bool _registerLifetime;
        private readonly Func<bool> _getDirectMode;
        private readonly string _rpcSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        private Timer? _pollTimer;
        private int _rpcPort;
        private int _daemonGeneration;
        private int _disposeState;
        private int _isPollCallback;
        private volatile bool _rpcReady;
        private volatile bool _handoff;
        private string _statusMessage = string.Empty;
        public DownloadManagerConfig Config => _config;
        public int CurrentRpcPort => _rpcPort;
        public bool IsAria2cRunning => _rpcReady && _daemon.IsRunning;
        private bool IsStopping => Volatile.Read(ref _disposeState) != 0 || _handoff;
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                foreach (EventHandler<string> handler in StatusMessageChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                    try { handler(this, value); } catch (Exception ex) { log.Error("Download status subscriber failed.", ex); }
            }
        }
        public event EventHandler<string>? StatusMessageChanged;
        public event EventHandler<DownloadTask>? DownloadCompleted;

        private Aria2cDownloadManager() : this(DbPath, DownloadManagerConfig.Instance) { }

        internal Aria2cDownloadManager(string dbPath, DownloadManagerConfig config, IAria2Daemon? daemon = null,
            IAria2RpcClient? rpcClient = null, DownloadReuseService? reuseService = null, bool enablePolling = true, bool registerLifetime = true, Func<bool>? directMode = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
            _store = new DownloadTaskStore(dbPath);
            _store.Initialize();
            _config = config;
            _rpcPort = config.RpcPort;
            _daemon = daemon ?? new Aria2Daemon();
            _rpcClient = rpcClient ?? new Aria2RpcClient(() => _rpcPort, _rpcSecret);
            _reuseService = reuseService ?? new DownloadReuseService();
            _rpcReady = daemon?.IsRunning == true;
            _enablePolling = enablePolling;
            _registerLifetime = registerLifetime;
            _getDirectMode = directMode ?? (() => UpdateNetworkConfig.Instance.DisableSystemProxyForUpdates);
            foreach (string path in _store.GetPendingPaths()) _reservedPaths.Add(Path.GetFullPath(path));
            config.PropertyChanged += OnConfigPropertyChanged;
            if (registerLifetime)
            {
                if (Application.Current != null)
                {
                    Application.Current.Exit += OnApplicationExit;
                    Application.Current.SessionEnding += OnApplicationSessionEnding;
                }
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            }
        }

        private void OnApplicationExit(object? sender, ExitEventArgs e) => Dispose();
        private void OnApplicationSessionEnding(object? sender, SessionEndingCancelEventArgs e) => Dispose();
        private void OnProcessExit(object? sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;
            _lifetime.Cancel();
            foreach (var task in _knownTasks.Values) task.Runtime.Stop(delete: false);
            Config.PropertyChanged -= OnConfigPropertyChanged;
            if (_registerLifetime)
            {
                if (Application.Current != null)
                {
                    Application.Current.Exit -= OnApplicationExit;
                    Application.Current.SessionEnding -= OnApplicationSessionEnding;
                }
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            }
            StopAria2cDaemon();
            _daemon.Dispose();
            _rpcClient.Dispose();
            _reuseService.Dispose();
            // Pending operations release their own cancellation sources after finishing.
            foreach (var task in _knownTasks.Values)
                _ = task.Runtime.Enqueue(() => { task.Runtime.ReleaseCancellationSources(); return Task.CompletedTask; });
            GC.SuppressFinalize(this);
        }

        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DownloadManagerConfig.MaxConcurrentTasks)) _copyGate.NotifyCapacityChanged();
            if (e.PropertyName is nameof(DownloadManagerConfig.MaxConcurrentTasks) or nameof(DownloadManagerConfig.EnableSpeedLimit) or nameof(DownloadManagerConfig.SpeedLimitMB))
                RunFireAndForget(async () =>
                {
                    if (IsStopping || !IsAria2cRunning) return;
                    var options = new Dictionary<string, string>
                    {
                        ["max-concurrent-downloads"] = Config.MaxConcurrentTasks.ToString(),
                        ["max-overall-download-limit"] = Config.EnableSpeedLimit ? $"{Config.SpeedLimitMB}M" : "0"
                    };
                    await _rpcClient.CallAsync("aria2.changeGlobalOption", options).ConfigureAwait(false);
                }, "Unable to apply download settings.");
        }

        private static async Task RunSafeAsync(Func<Task> action, string message)
        {
            try { await action().ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { log.Error(message, ex); }
        }
        private static void RunFireAndForget(Func<Task> action, string message) => _ = RunSafeAsync(action, message);
        private static Task OnUIAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) { action(); return Task.CompletedTask; }
            if (dispatcher.HasShutdownStarted) return Task.CompletedTask;
            return dispatcher.InvokeAsync(action).Task;
        }
        private static void PostUI(Action action) => RunFireAndForget(() => OnUIAsync(action), "Download UI update failed.");

        public void PreloadAria2cAsync() => RunFireAndForget(EnsureAria2cRunningAsync, "Unable to preload aria2c.");

        private async Task EnsureAria2cRunningAsync()
        {
            if (IsStopping) throw new OperationCanceledException();
            bool direct = _getDirectMode();
            if (_rpcReady && _daemon.IsRunningForNetworkMode(direct)) return;
            await _daemonLifecycleLock.WaitAsync(_lifetime.Token).ConfigureAwait(false);
            try
            {
                if (IsStopping) throw new OperationCanceledException();
                direct = _getDirectMode();
                if (_rpcReady && _daemon.IsRunningForNetworkMode(direct)) return;
                StopAria2cDaemon();
                _rpcPort = _daemon.PreparePort(Config.RpcPort);
                _daemon.Start(_rpcPort, _rpcSecret, Config, direct);
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                startup.CancelAfter(TimeSpan.FromSeconds(8));
                while (true)
                {
                    startup.Token.ThrowIfCancellationRequested();
                    try
                    {
                        await _rpcClient.CallAsync("aria2.getVersion", startup.Token).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        await Task.Delay(100, startup.Token).ConfigureAwait(false);
                    }
                }
                _rpcReady = true;
                int generation = Interlocked.Increment(ref _daemonGeneration);
                UpdateServiceStatus();
                foreach (var task in _activeTasks.Values.Where(task => task.Runtime.GetGids().Length > 0 && task.Runtime.DaemonGeneration != generation))
                    QueueStart(task, tryReuse: false, resume: false);
            }
            catch
            {
                StopAria2cDaemon();
                throw;
            }
            finally { _daemonLifecycleLock.Release(); }
        }

        public async Task StopDaemonForUpdateHandoffAsync()
        {
            _handoff = true;
            StopPolling();
            await _daemonLifecycleLock.WaitAsync().ConfigureAwait(false);
            try { await Task.Run(StopAria2cDaemon).ConfigureAwait(false); }
            finally { _daemonLifecycleLock.Release(); }
        }

        private void StopAria2cDaemon()
        {
            _rpcReady = false;
            StopPolling();
            _daemon.Stop(() =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                _rpcClient.CallAsync("aria2.forceShutdown", timeout.Token).GetAwaiter().GetResult();
            });
            UpdateServiceStatus();
        }

        private void UpdateServiceStatus() => StatusMessage = string.Format(Properties.Resources.Aria2cServiceStatus,
            IsAria2cRunning ? Properties.Resources.Aria2cConnected : Properties.Resources.Aria2cDisconnected, _rpcPort);
        private void StartPolling()
        {
            if (!_enablePolling || IsStopping) return;
            lock (_pollLock) _pollTimer ??= new Timer(_ => RunFireAndForget(() => PollAsync(waitForUpdates: false), "Download polling failed."), null, 0, 300);
        }
        private void StopPolling() { lock (_pollLock) { _pollTimer?.Dispose(); _pollTimer = null; } }

        public DownloadTask AddDownload(string url, string? savePath = null, string? authorization = null,
            Action<DownloadTask>? onCompleted = null, string? fileName = null)
            => AddVerifiedDownload(url, savePath, authorization, onCompleted, fileName, null);

        public DownloadTask AddVerifiedDownload(string url, string? savePath = null, string? authorization = null,
            Action<DownloadTask>? onCompleted = null, string? fileName = null, string? expectedSha256 = null)
        {
            ObjectDisposedException.ThrowIf(IsStopping, this);
            if (!Uri.TryCreate(url, UriKind.Absolute, out _)) throw new ArgumentException("A valid absolute download URL is required.", nameof(url));
            expectedSha256 = DownloadReuseService.NormalizeSha256(expectedSha256);
            string directory = Path.GetFullPath(savePath ?? Config.DefaultDownloadPath);
            Directory.CreateDirectory(directory);
            fileName = DownloadPathResolver.NormalizeFileName(fileName ?? DownloadPathResolver.GetFileNameFromUrl(url));
            DownloadTask task;
            lock (_pathLock)
            {
                string path = DownloadPathResolver.GetUniqueFilePath(directory, fileName, _reservedPaths.Contains);
                var entry = new DownloadEntry
                {
                    Url = url, FileName = Path.GetFileName(path), SavePath = path, DownloadDirectory = directory,
                    Status = (int)DownloadStatus.Waiting, Authorization = DownloadAuthorization.Encode(authorization),
                    ExpectedSha256 = expectedSha256, CreateTime = DateTime.Now
                };
                if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)) entry.DownloadDirectory = path;
                else entry.WorkingPath = Path.Combine(directory, $".cvdownload-{Guid.NewGuid():N}.part");
                entry.Id = _store.Insert(entry);
                task = CreateTask(entry);
                task.OnCompletedCallback = onCompleted;
                task.LocalReuseSourcePath = Path.Combine(directory, fileName);
                _reservedPaths.Add(path);
                _knownTasks[task.Id] = task;
            }
            PostUI(() => Tasks.Insert(0, task));
            QueueStart(task, tryReuse: true, resume: false);
            return task;
        }

        private static DownloadTask CreateTask(DownloadEntry entry) => new()
        {
            Id = entry.Id, Url = entry.Url, FileName = entry.FileName, SavePath = entry.SavePath,
            DownloadDirectory = entry.DownloadDirectory ?? Path.GetDirectoryName(entry.SavePath),
            WorkingPath = entry.WorkingPath, ContentSha256 = entry.ContentSha256,
            Status = (DownloadStatus)entry.Status, TotalBytes = entry.TotalBytes, DownloadedBytes = entry.DownloadedBytes,
            ProgressValue = entry.TotalBytes > 0 ? (int)Math.Clamp(entry.DownloadedBytes * 100 / entry.TotalBytes, 0, 100) : 0,
            CreateTime = entry.CreateTime, ErrorMessage = entry.ErrorMessage,
            Authorization = DownloadAuthorization.Decode(entry.Authorization), ExpectedSha256 = entry.ExpectedSha256
        };

        private DownloadTask Resolve(DownloadTask task) => _knownTasks.GetOrAdd(task.Id, task);
        internal Task WaitForTaskIdleAsync(DownloadTask task) => task.Runtime.Idle;
        private void QueueStart(DownloadTask task, bool tryReuse, bool resume)
        {
            if (IsStopping || task.Runtime.IsDeleted) return;
            task = Resolve(task);
            var run = task.Runtime.Start(_lifetime.Token);
            _activeTasks[task.Id] = task;
            PostUI(() => { if (task.Runtime.IsCurrent(run.Version)) { task.Status = DownloadStatus.Waiting; task.ErrorMessage = null; task.SpeedText = string.Empty; } });
            _ = task.Runtime.Enqueue(() => RunSafeAsync(() => StartTaskAsync(task, run.Version, run.Token, tryReuse, resume), "Download task operation failed."));
            StartPolling();
        }

        private async Task StartTaskAsync(DownloadTask task, long version, CancellationToken token, bool tryReuse, bool resume)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (!task.Runtime.IsRunning(version) || IsStopping) return;
                _store.UpdateStatus(task.Id, DownloadStatus.Waiting);
                // Recover a completed promotion if shutdown/cancellation happened before recording completion.
                if (task.WorkingPath != null && !File.Exists(task.WorkingPath) && File.Exists(task.SavePath) && task.ContentSha256 != null)
                {
                    string hash = await DownloadReuseService.ComputeSha256Async(task.SavePath, token).ConfigureAwait(false);
                    if (hash.Equals(task.ContentSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        long length = new FileInfo(task.SavePath).Length;
                        await CompleteTaskAsync(task, version, length, length).ConfigureAwait(false);
                        return;
                    }
                }
                if (tryReuse && !File.Exists(task.SavePath) && !File.Exists(task.WorkingPath ?? task.SavePath))
                {
                    string? source = task.LocalReuseSourcePath;
                    if (string.IsNullOrWhiteSpace(source) || source.Equals(task.SavePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(source) || File.Exists(source + ".aria2"))
                        source = _store.GetCompletedEntriesByUrl(task.Url).FirstOrDefault(entry => DownloadReuseService.CanReuseCompletedEntry(entry,
                            task.DownloadDirectory!, task.SavePath))?.SavePath;
                    if (source != null)
                    {
                        _localCopyTasks[task.Id] = task;
                        try
                        {
                            using var slot = await _copyGate.EnterAsync(() => Config.MaxConcurrentTasks, token).ConfigureAwait(false);
                            var validation = await _reuseService.TryValidateLocalFileAgainstRemoteAsync(task.Url, source, task.Authorization, token, task.ExpectedSha256).ConfigureAwait(false);
                            if (validation != null)
                            {
                                await UpdateTaskAsync(task, version, DownloadStatus.Downloading, validation.ContentLength ?? 0, 0, string.Empty, null).ConfigureAwait(false);
                                _store.UpdateStatus(task.Id, DownloadStatus.Downloading);
                                var copied = await LocalFileCopyService.CopyAsync(source, task.SavePath, validation.ContentLength ?? 0, progress =>
                                    PostUI(() => { if (task.Runtime.IsCurrent(version)) ApplyProgress(task, progress.TotalBytes, progress.CopiedBytes, progress.BytesPerSecond); }),
                                    token, validation.ContentSha256).ConfigureAwait(false);
                                task.ContentSha256 = copied.ContentSha256;
                                _store.UpdateContentHash(task.Id, copied.ContentSha256);
                                await CompleteTaskAsync(task, version, copied.TotalBytes, copied.CompletedBytes).ConfigureAwait(false);
                                return;
                            }
                        }
                        catch (Exception ex) when (!token.IsCancellationRequested)
                        {
                            // A failed validation/copy cannot delete the final destination or a callback's result.
                            log.Info($"Local reuse unavailable for task {task.Id}: {ex.Message}");
                            if (File.Exists(task.SavePath)) throw new IOException("The download destination already exists; it was preserved.", ex);
                        }
                        finally { _localCopyTasks.TryRemove(task.Id, out _); }
                    }
                }
                token.ThrowIfCancellationRequested();
                await EnsureAria2cRunningAsync().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!task.Runtime.IsRunning(version) || IsStopping) return;
                string[] existing = task.Runtime.DaemonGeneration == _daemonGeneration ? task.Runtime.GetGids() : Array.Empty<string>();
                if (resume && existing.Length > 0)
                {
                    try
                    {
                        foreach (string gid in existing) await _rpcClient.CallAsync("aria2.unpause", gid).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        _store.UpdateStatus(task.Id, DownloadStatus.Waiting);
                        StartPolling();
                        return;
                    }
                    catch (Aria2RpcException) { /* Removed/stale GIDs are recreated using their intact partial files. */ }
                }
                await RemoveGidsAsync(existing).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                string newGid = Guid.NewGuid().ToString("N")[..16];
                task.Gid = newGid;
                task.Runtime.SetGids(new[] { newGid }, _daemonGeneration);
                var options = new Dictionary<string, string> { ["dir"] = task.DownloadDirectory!, ["gid"] = newGid };
                if (!task.Url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                {
                    options["out"] = Path.GetFileName(task.WorkingPath ?? task.SavePath);
                    if (task.ExpectedSha256 != null) options["checksum"] = "sha-256=" + task.ExpectedSha256;
                }
                if (!string.IsNullOrWhiteSpace(task.Authorization) && task.Authorization.Contains(':'))
                {
                    string[] auth = task.Authorization.Split(':', 2);
                    options["http-user"] = auth[0]; options["http-passwd"] = auth[1];
                }
                // Observe the bounded addUri response even after cancellation, so an accepted GID can be removed.
                await _rpcClient.CallAsync("aria2.addUri", new[] { task.Url }, options).ConfigureAwait(false);
                if (!task.Runtime.IsRunning(version) || IsStopping)
                {
                    await RemoveGidsAsync(new[] { newGid }).ConfigureAwait(false);
                    task.Gid = null;
                    task.Runtime.SetGids(Array.Empty<string>(), _daemonGeneration);
                    return;
                }
                _store.UpdateStatus(task.Id, DownloadStatus.Waiting);
                StartPolling();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested || IsStopping) { }
            catch (Exception ex)
            {
                await RemoveGidsAsync(task.Runtime.GetGids()).ConfigureAwait(false);
                await FailTaskAsync(task, version, ex.Message).ConfigureAwait(false);
            }
            finally { _localCopyTasks.TryRemove(task.Id, out _); }
        }

        private async Task RemoveGidsAsync(string[] gids)
        {
            if (!_daemon.IsRunning) return;
            foreach (string gid in gids)
            {
                try { await _rpcClient.CallAsync("aria2.remove", gid).ConfigureAwait(false); }
                catch (Aria2RpcException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)) { }
                catch (Exception ex)
                {
                    log.Warn($"Unable to remove owned GID {gid}; stopping the owned daemon.", ex);
                    await _daemonLifecycleLock.WaitAsync().ConfigureAwait(false);
                    try { StopAria2cDaemon(); }
                    finally { _daemonLifecycleLock.Release(); }
                    if (!_activeTasks.IsEmpty) StartPolling();
                    return;
                }
            }
        }

        public void PauseDownload(DownloadTask task) => StopTask(task, remove: false);
        public void CancelDownload(DownloadTask task) => StopTask(task, remove: true);
        private void StopTask(DownloadTask requested, bool remove)
        {
            var task = Resolve(requested);
            if (!task.Runtime.IsRunning(task.Runtime.Version)) return;
            long version = task.Runtime.Stop(delete: false);
            PostUI(() => { if (task.Runtime.IsCurrent(version)) { task.Status = DownloadStatus.Paused; task.SpeedText = string.Empty; } });
            _ = task.Runtime.Enqueue(() => RunSafeAsync(async () =>
            {
                if (!task.Runtime.IsCurrent(version) || IsStopping) return;
                string[] gids = task.Runtime.DaemonGeneration == _daemonGeneration ? task.Runtime.GetGids() : Array.Empty<string>();
                try
                {
                    foreach (string gid in gids) await _rpcClient.CallAsync(remove ? "aria2.remove" : "aria2.pause", gid).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log.Warn("Unable to stop an owned download through RPC; stopping the owned daemon.", ex);
                    await _daemonLifecycleLock.WaitAsync().ConfigureAwait(false);
                    try { StopAria2cDaemon(); }
                    finally { _daemonLifecycleLock.Release(); }
                }
                if (remove) { task.Gid = null; task.Runtime.SetGids(Array.Empty<string>(), _daemonGeneration); }
                if (task.Runtime.IsCurrent(version))
                {
                    _store.UpdateBytes(task.Id, task.TotalBytes, task.DownloadedBytes);
                    _store.UpdateStatus(task.Id, DownloadStatus.Paused);
                    task.Runtime.WithCurrent(version, () => { _activeTasks.TryRemove(task.Id, out _); task.Runtime.ReleaseCancellationSources(); });
                }
                if (!_activeTasks.IsEmpty) StartPolling();
            }, "Unable to pause download task."));
        }
        public void ResumeDownload(DownloadTask task) => QueueStart(Resolve(task), tryReuse: true, resume: true);
        public void RetryDownload(DownloadTask task) => QueueStart(Resolve(task), tryReuse: true, resume: false);

        public void AutoRestartIncompleteDownloadsAsync() => RunFireAndForget(() => Task.Run(AutoRestartIncompleteDownloads), "Unable to restore downloads.");
        public void AutoRestartIncompleteDownloads()
        {
            if (IsStopping) return;
            foreach (var entry in _store.GetIncompleteEntries())
            {
                var task = _knownTasks.GetOrAdd(entry.Id, _ => CreateTask(entry));
                if (task.Runtime.IsRunning(task.Runtime.Version)) continue;
                PostUI(() => { if (!Tasks.Any(existing => existing.Id == task.Id)) Tasks.Insert(0, task); });
                QueueStart(task, tryReuse: false, resume: false);
            }
        }

        internal async Task PollAsync(bool waitForUpdates = true)
        {
            if (Interlocked.Exchange(ref _isPollCallback, 1) != 0) return;
            try
            {
                if (IsStopping) return;
                if (_activeTasks.IsEmpty)
                {
                    lock (_pollLock) { if (_activeTasks.IsEmpty) StopPolling(); }
                    UpdateServiceStatus();
                    return;
                }
                await EnsureAria2cRunningAsync().ConfigureAwait(false);
                var snapshots = _activeTasks.Values.Where(task => task.Runtime.Idle.IsCompleted && task.Runtime.IsRunning(task.Runtime.Version))
                    .SelectMany(task => task.Runtime.GetGids().Select(gid => (Task: task, Version: task.Runtime.Version, Gid: gid))).ToArray();
                long globalSpeed = 0;
                foreach (var chunk in snapshots.Chunk(64))
                {
                    var results = await _rpcClient.GetStatusesAsync(chunk.Select(snapshot => snapshot.Gid).ToArray(), TellStatusKeys, _lifetime.Token).ConfigureAwait(false);
                    var updates = new List<Task>();
                    for (int index = 0; index < results.Length; index++)
                    {
                        var snapshot = chunk[index]; var result = results[index];
                        globalSpeed += ParseLong(result.Status?["downloadSpeed"]);
                        updates.Add(snapshot.Task.Runtime.Enqueue(() => RunSafeAsync(() => ApplyStatusAsync(snapshot.Task, snapshot.Version, result), "Unable to apply download status.")));
                    }
                    // Completion may hash a large file. Its task worker must not hold up other downloads' polling.
                    if (waitForUpdates) await Task.WhenAll(updates).ConfigureAwait(false);
                }
                StatusMessage = string.Format(Properties.Resources.ActiveDownloads, _activeTasks.Count) +
                    (globalSpeed > 0 ? " | " + string.Format(Properties.Resources.GlobalSpeed, DownloadTask.FormatSpeed(globalSpeed)) : string.Empty);
            }
            catch (OperationCanceledException) when (IsStopping) { }
            catch (Exception ex)
            {
                _rpcReady = false;
                UpdateServiceStatus();
                log.Warn("Download service status failed.", ex);
                if (!_daemon.IsRunning)
                {
                    foreach (var task in _activeTasks.Values)
                    {
                        long version = task.Runtime.Version;
                        _ = task.Runtime.Enqueue(() => FailTaskAsync(task, version, ex.Message));
                    }
                }
            }
            finally { Volatile.Write(ref _isPollCallback, 0); }
        }

        private async Task ApplyStatusAsync(DownloadTask task, long version, Aria2StatusResult result)
        {
            if (IsStopping || !task.Runtime.IsRunning(version) || !task.Runtime.GetGids().Contains(result.Gid)) return;
            if (result.Status == null)
            {
                await FailTaskAsync(task, version, result.Error ?? "Download status is unavailable.").ConfigureAwait(false);
                return;
            }
            var status = result.Status;
            if (status["followedBy"] is JArray followed && followed.Count > 0)
            {
                task.Runtime.Follow(result.Gid, followed.Select(value => value.ToString()));
                task.Gid = task.Runtime.GetGids().FirstOrDefault();
                return;
            }
            var statuses = task.Runtime.ApplyStatus(result.Gid, status);
            if (status["status"]?.ToString() == "error")
            {
                await RemoveGidsAsync(task.Runtime.GetGids()).ConfigureAwait(false);
                await FailTaskAsync(task, version, status["errorMessage"]?.ToString() ?? "Download failed.").ConfigureAwait(false);
                return;
            }
            if (status["status"]?.ToString() is "paused" or "removed") { StopTask(task, remove: false); return; }
            long total = statuses.Sum(value => ParseLong(value["totalLength"]));
            long completed = statuses.Sum(value => ParseLong(value["completedLength"]));
            long speed = statuses.Sum(value => ParseLong(value["downloadSpeed"]));
            if (statuses.Length == task.Runtime.GetGids().Length && statuses.All(value => value["status"]?.ToString() == "complete"))
            {
                try { await ValidateAndCompleteAsync(task, version, statuses, total, completed).ConfigureAwait(false); }
                catch (Exception ex) { await FailTaskAsync(task, version, ex.Message).ConfigureAwait(false); }
                return;
            }
            string? name = statuses.Select(value => value["bittorrent"]?["info"]?["name"]?.ToString()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            DownloadStatus state = statuses.Any(value => value["status"]?.ToString() == "active") ? DownloadStatus.Downloading : DownloadStatus.Waiting;
            await UpdateTaskAsync(task, version, state, total, completed, DownloadTask.FormatSpeed(speed), null).ConfigureAwait(false);
            if (name != null && task.FileName != name)
            {
                string safeName = DownloadPathResolver.NormalizeFileName(name);
                await OnUIAsync(() => { if (task.Runtime.IsCurrent(version)) task.FileName = safeName; }).ConfigureAwait(false);
                _store.UpdateFileName(task.Id, safeName);
            }
        }

        private async Task ValidateAndCompleteAsync(DownloadTask task, long version, JObject[] statuses, long total, long completed)
        {
            bool torrent = task.Url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || statuses.Any(status => status["bittorrent"] != null);
            string[] files = statuses.SelectMany(status => status["files"] as JArray ?? new JArray())
                .Where(file => file["selected"]?.ToString() != "false").Select(file => file["path"]?.ToString())
                .Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => Path.GetFullPath(path!)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) files = new[] { task.WorkingPath ?? task.SavePath };
            string root = Path.GetFullPath(task.DownloadDirectory!) + Path.DirectorySeparatorChar;
            foreach (string path in files)
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || File.Exists(path + ".aria2"))
                    throw new InvalidDataException("Downloaded file is missing, incomplete, or outside the download directory.");
            long actual = files.Sum(path => new FileInfo(path).Length);
            if (actual <= 0 || (total > 0 && actual != total) || (total > 0 && completed != total))
                throw new InvalidDataException($"Downloaded file size mismatch: {actual}/{total} bytes.");
            if (!torrent)
            {
                string workingPath = task.WorkingPath ?? task.SavePath;
                if (files.Length != 1 || !files[0].Equals(Path.GetFullPath(workingPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("aria2 returned an unexpected output path.");
                CancellationToken token = task.Runtime.GetCancellationToken(version);
                using var slot = await _copyGate.EnterAsync(() => Config.MaxConcurrentTasks, token).ConfigureAwait(false);
                string hash = await DownloadReuseService.ComputeSha256Async(workingPath, token).ConfigureAwait(false);
                if (task.ExpectedSha256 != null && !hash.Equals(task.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Downloaded file SHA-256 verification failed.");
                if (!task.Runtime.IsRunning(version)) return;
                task.ContentSha256 = hash;
                _store.UpdateContentHash(task.Id, hash);
                if (task.WorkingPath != null) File.Move(workingPath, task.SavePath, overwrite: false);
            }
            else
            {
                string path = files.Length == 1 ? files[0] : Path.Combine(task.DownloadDirectory!, Path.GetRelativePath(task.DownloadDirectory!, files[0]).Split(Path.DirectorySeparatorChar)[0]);
                _store.UpdatePath(task.Id, path, Path.GetFileName(path));
                await OnUIAsync(() => { if (task.Runtime.IsCurrent(version)) { task.SavePath = path; task.FileName = Path.GetFileName(path); } }).ConfigureAwait(false);
            }
            await CompleteTaskAsync(task, version, actual, actual).ConfigureAwait(false);
        }

        private static long ParseLong(JToken? value) => long.TryParse(value?.ToString(), out long result) ? result : 0;
        private static void ApplyProgress(DownloadTask task, long total, long completed, long speed)
        {
            task.TotalBytes = total; task.DownloadedBytes = completed;
            task.ProgressValue = total > 0 ? (int)Math.Clamp(completed * 100 / total, 0, 100) : 0;
            task.SpeedText = DownloadTask.FormatSpeed(speed);
        }
        private static Task UpdateTaskAsync(DownloadTask task, long version, DownloadStatus status, long total, long completed, string speed, string? error) => OnUIAsync(() =>
        {
            if (!task.Runtime.IsCurrent(version)) return;
            task.Status = status; task.TotalBytes = total; task.DownloadedBytes = completed;
            task.ProgressValue = total > 0 ? (int)Math.Clamp(completed * 100 / total, 0, 100) : 0;
            task.SpeedText = speed; task.ErrorMessage = error;
        });
        private async Task CompleteTaskAsync(DownloadTask task, long version, long total, long completed)
        {
            if (IsStopping || !task.Runtime.Finish(version, DownloadStatus.Completed)) return;
            try { _store.MarkCompleted(task.Id, total, completed, DateTime.Now); }
            catch (Exception ex) { log.Error($"Unable to persist completed download {task.Id}; the completed file was preserved.", ex); }
            await UpdateTaskAsync(task, version, DownloadStatus.Completed, total, completed, string.Empty, null).ConfigureAwait(false);
            if (task.Runtime.WithCurrent(version, () =>
            {
                _activeTasks.TryRemove(task.Id, out _);
                _knownTasks.TryRemove(task.Id, out _);
                lock (_pathLock) _reservedPaths.Remove(Path.GetFullPath(task.SavePath));
                task.Runtime.ReleaseCancellationSources();
            })) NotifyCompleted(task);
        }
        private async Task FailTaskAsync(DownloadTask task, long version, string error)
        {
            if (IsStopping || !task.Runtime.Finish(version, DownloadStatus.Failed)) return;
            try { _store.UpdateStatus(task.Id, DownloadStatus.Failed, error); }
            catch (Exception ex) { log.Error($"Unable to persist failed download {task.Id}.", ex); }
            await UpdateTaskAsync(task, version, DownloadStatus.Failed, task.TotalBytes, task.DownloadedBytes, string.Empty, error).ConfigureAwait(false);
            if (task.Runtime.WithCurrent(version, () => { _activeTasks.TryRemove(task.Id, out _); task.Runtime.ReleaseCancellationSources(); })) NotifyCompleted(task);
        }
        private void NotifyCompleted(DownloadTask task)
        {
            if (IsStopping || task.Runtime.IsDeleted) return;
            try { task.OnCompletedCallback?.Invoke(task); }
            catch (Exception ex) { log.Error("Download completion callback failed.", ex); }
            foreach (EventHandler<DownloadTask> handler in DownloadCompleted?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { handler(this, task); } catch (Exception ex) { log.Error("Download completion subscriber failed.", ex); }
        }

        public void DeleteRecord(int id) => RunFireAndForget(() => DeleteRecordsAsync(new[] { id }), "Unable to delete download record.");
        public void DeleteRecords(int[] ids, bool deleteFiles = false) => RunFireAndForget(() => DeleteRecordsAsync(ids, deleteFiles), "Unable to delete download records.");
        public async Task DeleteRecordsAsync(int[] ids, bool deleteFiles = false)
        {
            ids = ids.Distinct().ToArray();
            if (ids.Length == 0) return;
            foreach (int id in ids)
                if (_knownTasks.TryGetValue(id, out var active)) active.Runtime.Stop(delete: true);
            var entries = await Task.Run(() => _store.GetEntries(ids)).ConfigureAwait(false);
            foreach (var entry in entries)
            {
                var task = _knownTasks.GetOrAdd(entry.Id, _ => CreateTask(entry));
                task.Runtime.Stop(delete: true);
                await task.Runtime.Enqueue(() => RunSafeAsync(async () =>
                {
                    if (task.Runtime.DaemonGeneration == _daemonGeneration)
                        await RemoveGidsAsync(task.Runtime.GetGids()).ConfigureAwait(false);
                    _activeTasks.TryRemove(task.Id, out _);
                    _localCopyTasks.TryRemove(task.Id, out _);
                    task.Runtime.ReleaseCancellationSources();
                    if (deleteFiles)
                        foreach (string path in new[] { task.SavePath, task.SavePath + ".aria2", task.WorkingPath, task.WorkingPath == null ? null : task.WorkingPath + ".aria2" }.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
                            try { if (File.Exists(path)) File.Delete(path); }
                            catch (Exception ex) { log.Warn($"Unable to delete requested download file: {path}", ex); }
                }, "Unable to stop deleted download task.")).ConfigureAwait(false);
            }
            await Task.Run(() => _store.DeleteMany(ids)).ConfigureAwait(false);
            await OnUIAsync(() =>
            {
                foreach (var task in Tasks.Where(task => ids.Contains(task.Id)).ToArray()) Tasks.Remove(task);
            }).ConfigureAwait(false);
            foreach (var entry in entries)
            {
                _knownTasks.TryRemove(entry.Id, out _);
                lock (_pathLock) _reservedPaths.Remove(Path.GetFullPath(entry.SavePath));
            }
        }
        public void ClearAllRecords() => RunFireAndForget(ClearAllRecordsAsync, "Unable to clear download records.");
        public async Task ClearAllRecordsAsync()
        {
            var entries = await Task.Run(_store.GetAllEntries).ConfigureAwait(false);
            // Delete the snapshot, so a download added while clearing is not accidentally removed.
            await DeleteRecordsAsync(entries.Select(entry => entry.Id).ToArray()).ConfigureAwait(false);
        }

        public int GetTotalCount(string? searchKeyword = null) => _store.GetTotalCount(searchKeyword);
        public void LoadRecords(string? searchKeyword = null, int pageSize = 20, int page = 1)
        {
            var records = ReadPage(searchKeyword, pageSize, page);
            OnUIAsync(() => ApplyPage(records)).GetAwaiter().GetResult();
        }
        public async Task<(int TotalCount, int Page)> LoadRecordsAsync(string? searchKeyword = null, int pageSize = 20, int page = 1, CancellationToken cancellationToken = default)
        {
            var records = await Task.Run(() => ReadPage(searchKeyword, pageSize, page), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await OnUIAsync(() => { cancellationToken.ThrowIfCancellationRequested(); ApplyPage(records); }).ConfigureAwait(false);
            return (records.TotalCount, records.Page);
        }
        private DownloadRecordPage ReadPage(string? keyword, int size, int page)
        {
            var records = _store.LoadPage(keyword, Math.Clamp(size, 1, 1000), Math.Max(1, page));
            foreach (var entry in records.Entries)
            {
                if (_knownTasks.ContainsKey(entry.Id) || entry.Status != (int)DownloadStatus.Completed) continue;
                if (Directory.Exists(entry.SavePath)) continue;
                if (!File.Exists(entry.SavePath))
                {
                    entry.Status = (int)DownloadStatus.FileDeleted;
                    _store.UpdateStatus(entry.Id, DownloadStatus.FileDeleted);
                    continue;
                }
                long length = new FileInfo(entry.SavePath).Length;
                if (length <= 0 || File.Exists(entry.SavePath + ".aria2") || (entry.TotalBytes > 0 && length != entry.TotalBytes))
                {
                    entry.Status = (int)DownloadStatus.Failed;
                    entry.ErrorMessage = "Downloaded file is empty or its size has changed.";
                    _store.UpdateStatus(entry.Id, DownloadStatus.Failed, entry.ErrorMessage);
                }
                else if (entry.TotalBytes <= 0 || entry.DownloadedBytes <= 0)
                {
                    entry.TotalBytes = entry.DownloadedBytes = length;
                    _store.UpdateBytes(entry.Id, length, length);
                }
            }
            return records;
        }
        private void ApplyPage(DownloadRecordPage records)
        {
            Tasks.Clear();
            foreach (var entry in records.Entries)
            {
                DownloadTask task = _knownTasks.TryGetValue(entry.Id, out var known) ? known : CreateTask(entry);
                if (task.Runtime.IsDeleted) continue;
                if (task.Status is DownloadStatus.Waiting or DownloadStatus.Downloading or DownloadStatus.Paused or DownloadStatus.Failed)
                    task = _knownTasks.GetOrAdd(task.Id, task);
                Tasks.Add(task);
            }
        }
    }
}
