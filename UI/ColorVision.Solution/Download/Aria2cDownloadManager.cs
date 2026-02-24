using ColorVision.Common.MVVM;
using log4net;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        public DownloadStatus Status { get => _Status; set { _Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); } }
        private DownloadStatus _Status;

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

        public CancellationTokenSource? CancellationTokenSource { get; set; }

        private static string FormatBytes(long bytes)
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
        private readonly SemaphoreSlim _semaphore;
        private readonly string _aria2cPath;

        /// <summary>
        /// Fired when a download task completes (success or failure)
        /// </summary>
        public event EventHandler<DownloadTask>? DownloadCompleted;

        public DownloadManagerConfig Config => DownloadManagerConfig.Instance;

        private Aria2cDownloadManager()
        {
            Directory.CreateDirectory(DirectoryPath);
            _semaphore = new SemaphoreSlim(Config.MaxConcurrentTasks, 16);
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
            task.CancellationTokenSource = new CancellationTokenSource();
            try
            {
                await _semaphore.WaitAsync(task.CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Paused;
                    task.SpeedText = string.Empty;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Paused);
                return;
            }
            try
            {
                Application.Current?.Dispatcher.BeginInvoke(() => task.Status = DownloadStatus.Downloading);
                _activeTasks[task.Id] = task;

                UpdateEntryStatus(task.Id, DownloadStatus.Downloading);

                string args = BuildAria2cArgs(task, authorization);
                await RunAria2cAsync(task, args);
            }
            catch (OperationCanceledException)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Paused;
                    task.SpeedText = string.Empty;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Paused);
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
            finally
            {
                _activeTasks.TryRemove(task.Id, out _);
                _semaphore.Release();
            }
        }

        private string BuildAria2cArgs(DownloadTask task, string? authorization)
        {
            string dir = Path.GetDirectoryName(task.SavePath) ?? Config.DefaultDownloadPath;
            string fileName = Path.GetFileName(task.SavePath);

            string args = $"\"{task.Url}\" -d \"{dir}\" -o \"{fileName}\" -c --auto-file-renaming=false --allow-overwrite=true --summary-interval=1 --enable-color=false";

            if (Config.EnableSpeedLimit)
            {
                args += $" --max-download-limit={Config.SpeedLimitMB}M";
            }

            string auth = authorization ?? Config.Authorization;
            if (!string.IsNullOrWhiteSpace(auth) && auth.Contains(':'))
            {
                string[] parts = auth.Split(':', 2);
                args += $" --http-user=\"{parts[0]}\" --http-passwd=\"{parts[1]}\"";
            }

            return args;
        }

        private async Task RunAria2cAsync(DownloadTask task, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _aria2cPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = Task.Run(() => ParseAria2cOutput(process, task));
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            string errorOutput = await errorTask;

            if (task.CancellationTokenSource?.IsCancellationRequested == true)
            {
                try { if (!process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { /* process already exited */ }
                throw new OperationCanceledException();
            }

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Completed;
                    task.ProgressValue = 100;
                    task.SpeedText = string.Empty;
                });
                UpdateEntryCompleted(task);
                DownloadCompleted?.Invoke(this, task);
            }
            else
            {
                string error = !string.IsNullOrWhiteSpace(errorOutput) ? errorOutput.Trim() : $"aria2c exited with code {process.ExitCode}";
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    task.Status = DownloadStatus.Failed;
                    task.ErrorMessage = error;
                    task.SpeedText = string.Empty;
                });
                UpdateEntryStatus(task.Id, DownloadStatus.Failed, error);
                DownloadCompleted?.Invoke(this, task);
            }
        }

        private void ParseAria2cOutput(Process process, DownloadTask task)
        {
            // aria2c outputs progress using \r (carriage return) to overwrite the same line.
            // ReadLine() waits for \n which never comes during progress updates.
            // We must read char-by-char and treat both \r and \n as line terminators.
            var sizeRegex = new Regex(@"\[#\w+\s+(\d+(?:\.\d+)?)(Ki|Mi|Gi)?B/(\d+(?:\.\d+)?)(Ki|Mi|Gi)?B", RegexOptions.Compiled);
            var progressRegex = new Regex(@"\((\d+)%\)", RegexOptions.Compiled);
            var speedRegex = new Regex(@"DL:(\d+(?:\.\d+)?)(Ki|Mi|Gi)?B", RegexOptions.Compiled);

            try
            {
                var reader = process.StandardOutput;
                var sb = new System.Text.StringBuilder(256);
                var buffer = new char[1];

                while (reader.Read(buffer, 0, 1) > 0)
                {
                    if (task.CancellationTokenSource?.IsCancellationRequested == true)
                    {
                        try { if (!process.HasExited) process.Kill(); }
                        catch (InvalidOperationException) { /* process already exited */ }
                        return;
                    }

                    char c = buffer[0];
                    if (c == '\r' || c == '\n')
                    {
                        if (sb.Length == 0) continue;
                        string line = sb.ToString();
                        sb.Clear();

                        var progressMatch = progressRegex.Match(line);
                        if (progressMatch.Success && int.TryParse(progressMatch.Groups[1].Value, out int progress))
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() => task.ProgressValue = progress);
                        }

                        var sizeMatch = sizeRegex.Match(line);
                        if (sizeMatch.Success)
                        {
                            long downloaded = ParseSizeToBytes(sizeMatch.Groups[1].Value, sizeMatch.Groups[2].Value);
                            long total = ParseSizeToBytes(sizeMatch.Groups[3].Value, sizeMatch.Groups[4].Value);
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                task.DownloadedBytes = downloaded;
                                task.TotalBytes = total;
                            });
                        }

                        var speedMatch = speedRegex.Match(line);
                        if (speedMatch.Success)
                        {
                            long speed = ParseSizeToBytes(speedMatch.Groups[1].Value, speedMatch.Groups[2].Value);
                            string speedText = FormatSpeed(speed);
                            Application.Current?.Dispatcher.BeginInvoke(() => task.SpeedText = speedText);
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Parse output error: {ex.Message}");
            }
        }

        private static long ParseSizeToBytes(string value, string unit)
        {
            if (!double.TryParse(value, out double size)) return 0;
            return unit switch
            {
                "Ki" => (long)(size * 1024),
                "Mi" => (long)(size * 1024 * 1024),
                "Gi" => (long)(size * 1024 * 1024 * 1024),
                _ => (long)size
            };
        }

        private static string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "0 B/s";
            if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
            if (bytesPerSecond < 1024L * 1024 * 1024) return $"{bytesPerSecond / 1024.0 / 1024.0:F2} MB/s";
            return $"{bytesPerSecond / 1024.0 / 1024.0 / 1024.0:F2} GB/s";
        }

        public void CancelDownload(DownloadTask task)
        {
            task.CancellationTokenSource?.Cancel();
        }

        public void RetryDownload(DownloadTask task)
        {
            task.Status = DownloadStatus.Waiting;
            task.ProgressValue = 0;
            task.ErrorMessage = null;
            task.SpeedText = string.Empty;
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
                task.CancellationTokenSource?.Cancel();
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
                    task.CancellationTokenSource?.Cancel();
                    Tasks.Remove(task);
                }
            });
        }

        public void ClearAllRecords()
        {
            using var db = CreateDbClient();
            db.Deleteable<DownloadEntry>().ExecuteCommand();
            foreach (var task in _activeTasks.Values)
                task.CancellationTokenSource?.Cancel();
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
