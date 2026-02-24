using ColorVision.Common.MVVM;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Solution.Download
{
    public class DownloadTask : ViewModelBase
    {
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;

        public string Url { get => _Url; set { _Url = value; OnPropertyChanged(); } }
        private string _Url = string.Empty;

        public string FileName { get => _FileName; set { _FileName = value; OnPropertyChanged(); } }
        private string _FileName = string.Empty;

        public string SavePath { get => _SavePath; set { _SavePath = value; OnPropertyChanged(); } }
        private string _SavePath = string.Empty;

        public DownloadStatus Status { get => _Status; set { _Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(IsDownloading)); } }
        private DownloadStatus _Status;

        public bool IsDownloading => Status == DownloadStatus.Downloading || Status == DownloadStatus.Waiting;

        public string StatusText => Status switch
        {
            DownloadStatus.Waiting => Properties.Resources.Waiting,
            DownloadStatus.Downloading => Properties.Resources.Downloading,
            DownloadStatus.Completed => Properties.Resources.Completed,
            DownloadStatus.Failed => Properties.Resources.Failed,
            DownloadStatus.Paused => Properties.Resources.Paused,
            DownloadStatus.FileDeleted => Properties.Resources.FileDeleted,
            _ => Status.ToString()
        };

        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; OnPropertyChanged(); } }
        private int _ProgressValue;

        public long TotalBytes { get => _TotalBytes; set { _TotalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalBytesText)); } }
        private long _TotalBytes;

        public long DownloadedBytes { get => _DownloadedBytes; set { _DownloadedBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadedBytesText)); } }
        private long _DownloadedBytes;

        public string SpeedText { get => _SpeedText; set { _SpeedText = value; OnPropertyChanged(); } }
        private string _SpeedText = string.Empty;

        public string? ErrorMessage { get => _ErrorMessage; set { _ErrorMessage = value; OnPropertyChanged(); } }
        private string? _ErrorMessage;

        public DateTime CreateTime { get => _CreateTime; set { _CreateTime = value; OnPropertyChanged(); } }
        private DateTime _CreateTime = DateTime.Now;

        public string TotalBytesText => FormatBytes(TotalBytes);
        public string DownloadedBytesText => FormatBytes(DownloadedBytes);

        /// <summary>
        /// The aria2c GID for this download (used with JSON-RPC)
        /// </summary>
        public string? Gid { get; set; }

        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:F2} {sizes[order]}";
        }

        public static string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "0 B/s";
            if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
            if (bytesPerSecond < 1024L * 1024 * 1024) return $"{bytesPerSecond / 1024.0 / 1024.0:F2} MB/s";
            return $"{bytesPerSecond / 1024.0 / 1024.0 / 1024.0:F2} GB/s";
        }
    }

    public class Aria2cDownloadManager
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(Aria2cDownloadManager));

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
        private readonly string _aria2cPath;

        // JSON-RPC state
        private Process? _aria2cProcess;
        private readonly object _processLock = new();
        private readonly HttpClient _httpClient = new();
        private const int RpcPort = 6800;
        private const string RpcSecret = "ColorVisionDL";
        private string RpcUrl => $"http://127.0.0.1:{RpcPort}/jsonrpc";
        private string WsUrl => $"ws://127.0.0.1:{RpcPort}/jsonrpc";
        private int _rpcRequestId;
        private Timer? _pollTimer;

        // WebSocket state
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _wsCts;
        private Task? _wsListenTask;

        /// <summary>
        /// Fired when a download task completes (success or failure)
        /// </summary>
        public event EventHandler<DownloadTask>? DownloadCompleted;

        public DownloadManagerConfig Config => DownloadManagerConfig.Instance;

        private Aria2cDownloadManager()
        {
            Directory.CreateDirectory(DirectoryPath);
            _aria2cPath = FindAria2c();
            InitializeDatabase();

            // Clean up aria2c process when the application exits
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
        }

        /// <summary>
        /// Cleanly shut down the aria2c daemon and release resources.
        /// Called automatically on process exit.
        /// </summary>
        public void Dispose()
        {
            StopAria2cDaemon();
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

        #region aria2c RPC Daemon

        private async Task EnsureAria2cRunningAsync()
        {
            lock (_processLock)
            {
                if (_aria2cProcess != null && !_aria2cProcess.HasExited)
                    return;
            }

            await StartAria2cDaemonAsync();
        }

        private async Task StartAria2cDaemonAsync()
        {
            lock (_processLock)
            {
                if (_aria2cProcess != null && !_aria2cProcess.HasExited)
                    return;

                string args = $"--enable-rpc --rpc-listen-port={RpcPort} --rpc-secret={RpcSecret} --rpc-listen-all=false --enable-color=false -c --auto-file-renaming=false --allow-overwrite=true --summary-interval=0 -j {Config.MaxConcurrentTasks}";

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
                await Task.Delay(200);
                try
                {
                    var response = await RpcCallAsync("aria2.getVersion", Array.Empty<object>());
                    if (response != null)
                    {
                        log.Info("aria2c RPC daemon started successfully");
                        await ConnectWebSocketAsync();
                        return;
                    }
                }
                catch { }
            }

            log.Error("Failed to start aria2c RPC daemon");
            throw new Exception("Failed to start aria2c RPC daemon");
        }

        private void StopAria2cDaemon()
        {
            StopPolling();
            DisconnectWebSocket();
            lock (_processLock)
            {
                if (_aria2cProcess != null)
                {
                    try
                    {
                        if (!_aria2cProcess.HasExited)
                        {
                            // Try graceful shutdown via RPC on a thread pool thread to avoid deadlock
                            try
                            {
                                Task.Run(async () =>
                                {
                                    try { await RpcCallAsync("aria2.shutdown", new object[] { $"token:{RpcSecret}" }); }
                                    catch { }
                                }).Wait(2000);
                            }
                            catch { }

                            if (!_aria2cProcess.WaitForExit(3000))
                                _aria2cProcess.Kill();
                        }
                    }
                    catch (InvalidOperationException) { }
                    finally
                    {
                        _aria2cProcess.Dispose();
                        _aria2cProcess = null;
                    }
                }
            }
        }

        #region WebSocket Event Subscription

        private async Task ConnectWebSocketAsync()
        {
            DisconnectWebSocket();

            _wsCts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            try
            {
                await _webSocket.ConnectAsync(new Uri(WsUrl), _wsCts.Token);
                log.Info("WebSocket connected to aria2c RPC");
                _wsListenTask = Task.Run(() => WebSocketListenLoop(_wsCts.Token));
            }
            catch (Exception ex)
            {
                log.Warn($"WebSocket connection failed, falling back to polling: {ex.Message}");
                StartPolling();
            }
        }

        private void DisconnectWebSocket()
        {
            try
            {
                _wsCts?.Cancel();
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", CancellationToken.None)
                        .Wait(2000);
                }
            }
            catch { }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
                _wsCts?.Dispose();
                _wsCts = null;
            }
        }

        private async Task WebSocketListenLoop(CancellationToken ct)
        {
            var buffer = new byte[4096];
            var messageBuffer = new StringBuilder();

            while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    messageBuffer.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return;
                        messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    string message = messageBuffer.ToString();
                    HandleWebSocketMessage(message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        log.Debug($"WebSocket receive error: {ex.Message}");
                    break;
                }
            }
        }

        private void HandleWebSocketMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);
                string? method = json["method"]?.ToString();

                if (method == null) return;

                var gidToken = json["params"]?[0]?["gid"];
                string? gid = gidToken?.ToString();
                if (string.IsNullOrEmpty(gid)) return;

                var task = _activeTasks.Values.FirstOrDefault(t => t.Gid == gid);
                if (task == null) return;

                if (method.StartsWith("aria2.onDownload"))
                {
                    _ = Task.Run(() => PollTaskStatus(task));
                }
            }
            catch (Exception ex)
            {
                log.Debug($"WebSocket message handling error: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetch the latest status of a single task via HTTP RPC and update accordingly
        /// </summary>
        private async Task PollTaskStatus(DownloadTask task)
        {
            if (string.IsNullOrEmpty(task.Gid)) return;

            try
            {
                var status = await RpcCallAsync("aria2.tellStatus",
                    new object[] { $"token:{RpcSecret}", task.Gid,
                        new[] { "status", "totalLength", "completedLength", "downloadSpeed", "errorCode", "errorMessage" } });

                if (status == null) return;

                string? rpcStatus = status["result"]?["status"]?.ToString();
                long.TryParse(status["result"]?["totalLength"]?.ToString(), out long totalLength);
                long.TryParse(status["result"]?["completedLength"]?.ToString(), out long completedLength);
                long.TryParse(status["result"]?["downloadSpeed"]?.ToString(), out long downloadSpeed);

                int progress = totalLength > 0 ? (int)(completedLength * 100 / totalLength) : 0;
                string speedText = DownloadTask.FormatSpeed(downloadSpeed);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    task.ProgressValue = progress;
                    task.TotalBytes = totalLength;
                    task.DownloadedBytes = completedLength;
                    task.SpeedText = speedText;
                });

                if (rpcStatus == "complete")
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        task.TotalBytes = totalLength;
                        task.Status = DownloadStatus.Completed;
                        task.SpeedText = string.Empty;
                    });
                    UpdateEntryCompleted(task);
                    _activeTasks.TryRemove(task.Id, out _);
                    DownloadCompleted?.Invoke(this, task);
                }
                else if (rpcStatus == "error")
                {
                    string? errorMsg = status["result"]?["errorMessage"]?.ToString();
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        task.Status = DownloadStatus.Failed;
                        task.ErrorMessage = errorMsg ?? "Unknown error";
                        task.SpeedText = string.Empty;
                    });
                    UpdateEntryStatus(task.Id, DownloadStatus.Failed, errorMsg);
                    _activeTasks.TryRemove(task.Id, out _);
                    DownloadCompleted?.Invoke(this, task);
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

        #endregion

        private void StartPolling()
        {
            _pollTimer ??= new Timer(PollCallback, null, 0, 200);
        }

        private void StopPolling()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        private async void PollCallback(object? state)
        {
            try
            {
                var activeTasks = _activeTasks.Values.ToArray();
                if (activeTasks.Length == 0)
                {
                    StopPolling();
                    return;
                }
                foreach (var task in activeTasks)
                {
                    await PollTaskStatus(task);
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Poll callback error: {ex.Message}");
            }
        }

        private async Task<JObject?> RpcCallAsync(string method, object[] parameters)
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

            using var response = await _httpClient.PostAsync(RpcUrl, content).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JObject.Parse(responseBody);
        }

        #endregion

        /// <summary>
        /// Add a download task with default settings
        /// </summary>
        public DownloadTask AddDownload(string url, string? savePath = null, string? authorization = null)
        {
            string targetDir = savePath ?? Config.DefaultDownloadPath;
            Directory.CreateDirectory(targetDir);

            string fileName = GetFileNameFromUrl(url);
            string filePath = Path.Combine(targetDir, fileName);

            var entry = new DownloadEntry
            {
                Url = url,
                FileName = fileName,
                SavePath = filePath,
                Status = (int)DownloadStatus.Waiting,
                CreateTime = DateTime.Now
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
                CreateTime = entry.CreateTime
            };

            _activeTasks.AddOrUpdate(task.Id, task, (key, old) => task);
            Application.Current.Dispatcher.Invoke(() => Tasks.Insert(0, task));

            _ = StartDownloadAsync(task, authorization);

            return task;
        }

        private async Task StartDownloadAsync(DownloadTask task, string? authorization = null)
        {
            try
            {
                await EnsureAria2cRunningAsync();
                StartPolling(); // Fallback polling in case WebSocket is not connected

                Application.Current?.Dispatcher.BeginInvoke(() => task.Status = DownloadStatus.Downloading);
                UpdateEntryStatus(task.Id, DownloadStatus.Downloading);

                // Build options for this download
                string dir = Path.GetDirectoryName(task.SavePath) ?? Config.DefaultDownloadPath;
                string fileName = Path.GetFileName(task.SavePath);

                var options = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["dir"] = dir,
                    ["out"] = fileName,
                };

                string auth = authorization ?? Config.Authorization;
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
                log.Error($"Download failed: {ex.Message}", ex);
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Failed;
                    task.ErrorMessage = ex.Message;
                    task.SpeedText = string.Empty;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Failed, ex.Message);
                DownloadCompleted?.Invoke(this, task);
            }
        }

        public void CancelDownload(DownloadTask task)
        {
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
        }

        public void RetryDownload(DownloadTask task)
        {
            task.Status = DownloadStatus.Waiting;
            task.ProgressValue = 0;
            task.ErrorMessage = null;
            task.SpeedText = string.Empty;
            task.Gid = null;
            UpdateEntryStatus(task.Id, DownloadStatus.Waiting);
            _ = StartDownloadAsync(task);
        }

        public void DeleteRecord(int id)
        {
            using var db = CreateDbClient();
            db.Deleteable<DownloadEntry>().In(id).ExecuteCommand();
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                if (!string.IsNullOrEmpty(task.Gid))
                    TryRemoveGidAsync(task.Gid);
                _activeTasks.TryRemove(task.Id, out _);
                Application.Current.Dispatcher.Invoke(() => Tasks.Remove(task));
            }
        }

        public void DeleteRecords(int[] ids)
        {
            using var db = CreateDbClient();
            db.Deleteable<DownloadEntry>().In(ids).ExecuteCommand();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var toRemove = Tasks.Where(t => ids.Contains(t.Id)).ToList();
                foreach (var task in toRemove)
                {
                    if (!string.IsNullOrEmpty(task.Gid))
                        TryRemoveGidAsync(task.Gid);
                    _activeTasks.TryRemove(task.Id, out _);
                    Tasks.Remove(task);
                }
            });
        }

        public void ClearAllRecords()
        {
            using var db = CreateDbClient();
            db.Deleteable<DownloadEntry>().ExecuteCommand();
            foreach (var task in _activeTasks.Values)
            {
                if (!string.IsNullOrEmpty(task.Gid))
                    TryRemoveGidAsync(task.Gid);
            }
            _activeTasks.Clear();
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
                            ErrorMessage = entry.ErrorMessage
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

        private static string GetFileNameFromUrl(string url)
        {
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
    }
}
