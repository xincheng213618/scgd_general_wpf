using ColorVision.Common.MVVM;
using log4net;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
        private int _rpcRequestId;
        private Timer? _pollTimer;

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
                        StartPolling();
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
            lock (_processLock)
            {
                if (_aria2cProcess != null)
                {
                    try
                    {
                        if (!_aria2cProcess.HasExited)
                        {
                            // Try graceful shutdown via RPC first
                            try { _ = RpcCallAsync("aria2.shutdown", new object[] { $"token:{RpcSecret}" }).Result; }
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

        private void StartPolling()
        {
            _pollTimer ??= new Timer(PollCallback, null, 500, 500);
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
                    // No active downloads, stop daemon
                    StopAria2cDaemon();
                    return;
                }

                foreach (var task in activeTasks)
                {
                    if (string.IsNullOrEmpty(task.Gid)) continue;

                    try
                    {
                        var status = await RpcCallAsync("aria2.tellStatus",
                            new object[] { $"token:{RpcSecret}", task.Gid,
                                new[] { "status", "totalLength", "completedLength", "downloadSpeed", "errorCode", "errorMessage" } });

                        if (status == null) continue;

                        var result = status.Value;
                        string? rpcStatus = result.GetProperty("result").GetProperty("status").GetString();
                        long totalLength = long.Parse(result.GetProperty("result").GetProperty("totalLength").GetString() ?? "0");
                        long completedLength = long.Parse(result.GetProperty("result").GetProperty("completedLength").GetString() ?? "0");
                        long downloadSpeed = long.Parse(result.GetProperty("result").GetProperty("downloadSpeed").GetString() ?? "0");

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
                                task.Status = DownloadStatus.Completed;
                                task.ProgressValue = 100;
                                task.SpeedText = string.Empty;
                            });
                            UpdateEntryCompleted(task);
                            _activeTasks.TryRemove(task.Id, out _);
                            DownloadCompleted?.Invoke(this, task);
                        }
                        else if (rpcStatus == "error")
                        {
                            string? errorMsg = result.GetProperty("result").GetProperty("errorMessage").GetString();
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
            }
            catch (Exception ex)
            {
                log.Debug($"Poll callback error: {ex.Message}");
            }
        }

        private async Task<JsonElement?> RpcCallAsync(string method, object[] parameters)
        {
            int id = Interlocked.Increment(ref _rpcRequestId);
            var request = new
            {
                jsonrpc = "2.0",
                id = id.ToString(),
                method,
                @params = parameters
            };

            string json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(RpcUrl, content).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<JsonElement>(responseBody);
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

            Application.Current.Dispatcher.Invoke(() => Tasks.Insert(0, task));

            _ = StartDownloadAsync(task, authorization);

            return task;
        }

        private async Task StartDownloadAsync(DownloadTask task, string? authorization = null)
        {
            try
            {
                await EnsureAria2cRunningAsync();

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
                    string? gid = response.Value.GetProperty("result").GetString();
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
                _ = RpcCallAsync("aria2.remove", new object[] { $"token:{RpcSecret}", task.Gid });
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
                {
                    try { _ = RpcCallAsync("aria2.remove", new object[] { $"token:{RpcSecret}", task.Gid }); } catch { }
                }
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
                    {
                        try { _ = RpcCallAsync("aria2.remove", new object[] { $"token:{RpcSecret}", task.Gid }); } catch { }
                    }
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
                {
                    try { _ = RpcCallAsync("aria2.remove", new object[] { $"token:{RpcSecret}", task.Gid }); } catch { }
                }
            }
            _activeTasks.Clear();
            Application.Current.Dispatcher.Invoke(() => Tasks.Clear());
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
