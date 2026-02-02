#pragma warning disable
using ColorVision.UI;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Database.SqliteLog
{
    public class SqliteLogManager : AppenderSkeleton, IDisposable
    {
        private static SqliteLogManager _instance;
        private static readonly object _locker = new();

        public static SqliteLogManager GetInstance()
        {
            lock (_locker)
            {
                _instance ??= new SqliteLogManager();
                return _instance;
            }
        }

        public static string DirectoryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Log");

        // 固定当前正在使用的数据库文件名，不论月份，只通过大小控制轮转
        // 如果你需要同时按月分，可以在 GetActiveDbPath 里加逻辑，这里简化为单文件轮转
        public static string SqliteDbPath { get; set; } = Path.Combine(DirectoryPath, "SqliteLogs.db");

        public static SqliteLogManagerConfig Config => ConfigService.Instance.GetRequiredService<SqliteLogManagerConfig>();

        private BlockingCollection<LogEntry> _logQueue;
        private CancellationTokenSource _cts;
        private Task _writeTask;
        private bool _isEnabled = false;

        private const int BatchSize = 200;
        private const int FlushIntervalMs = 2000;

        Hierarchy _hierarchy;

        public SqliteLogManager()
        {
            Config.SqliteLogEnabledChanged += OnSqliteLogEnabledChanged;
            if (Config.IsEnabled) Enable();
        }

        private void OnSqliteLogEnabledChanged(object? sender, bool enabled)
        {
            if (enabled) Enable();
            else Disable();
        }

        private void Enable()
        {
            if (_isEnabled) return;
            lock (_locker)
            {
                if (_isEnabled) return;
                _isEnabled = true;

                Directory.CreateDirectory(DirectoryPath);

                _logQueue = new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>());
                _cts = new CancellationTokenSource();

                _hierarchy = (Hierarchy)LogManager.GetRepository();
                if (!AppenderExists(_hierarchy, this))
                {
                    _hierarchy.Root.AddAppender(this);
                    _hierarchy.Configured = true;
                    _hierarchy.RaiseConfigurationChanged(EventArgs.Empty);
                }

                _writeTask = Task.Factory.StartNew(ProcessQueue, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        private void Disable()
        {
            if (!_isEnabled) return;
            lock (_locker)
            {
                if (!_isEnabled) return;
                _isEnabled = false;

                _hierarchy?.Root.RemoveAppender(this);

                _logQueue?.CompleteAdding();
                try { _writeTask?.Wait(2000); } catch { }

                _cts?.Cancel();
                _cts?.Dispose();
                _logQueue?.Dispose();
            }
        }

                _cts?.Cancel();
                _cts?.Dispose();
                _logQueue?.Dispose();
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!_isEnabled || _logQueue == null || _logQueue.IsAddingCompleted) return;

            var entry = new LogEntry
            {
                Date = loggingEvent.TimeStamp,
                Thread = loggingEvent.ThreadName ?? string.Empty,
                Level = loggingEvent.Level?.Name ?? string.Empty,
                Logger = loggingEvent.LoggerName ?? string.Empty,
                Message = loggingEvent.RenderedMessage ?? string.Empty,
                Exception = loggingEvent.ExceptionObject?.ToString() ?? string.Empty,
            };

            _logQueue.TryAdd(entry);
        }

        private void ProcessQueue()
        {
            var buffer = new List<LogEntry>(BatchSize);

            while (!_cts.Token.IsCancellationRequested && !_logQueue.IsCompleted)
            {
                try
                {
                    LogEntry entry;
                    if (_logQueue.TryTake(out entry, FlushIntervalMs, _cts.Token))
                    {
                        buffer.Add(entry);
                    }

                    if (buffer.Count >= BatchSize || (buffer.Count > 0 && _logQueue.Count == 0))
                    {
                        WriteBatchToDb(buffer);
                        buffer.Clear();

                        // 写入完成后，检查大小并执行轮转逻辑
                        CheckAndRotateDb();
                    }
                }
                catch (OperationCanceledException)
                {
                    if (buffer.Count > 0) WriteBatchToDb(buffer);
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SqliteLogManager] Error: {ex.Message}");
                }
            }
        }

        private void WriteBatchToDb(List<LogEntry> logs)
        {
            if (logs.Count == 0) return;

            // 使用短连接
            using (var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath};Cache Size=2000;",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            }))
            {
                // WAL模式性能优化
                db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
                db.Ado.ExecuteCommand("PRAGMA synchronous=NORMAL;");
                db.CodeFirst.InitTables<LogEntry>();

                try
                {
                    db.BeginTran();
                    db.Insertable(logs).ExecuteCommand();
                    db.CommitTran();
                }
                catch
                {
                    db.RollbackTran();
                }

                // 强制 Checkpoint，确保 WAL 数据合并到 DB，方便后续移动文件
                // 这一步在即将检查文件大小时很有用
                // db.Ado.ExecuteCommand("PRAGMA wal_checkpoint(TRUNCATE);"); 
            }
        }

        /// <summary>
        /// 检查当前数据库大小，如果超限则进行轮转（压缩或删除）
        /// </summary>
        private void CheckAndRotateDb()
        {
            try
            {
                var fileInfo = new FileInfo(SqliteDbPath);
                if (!fileInfo.Exists) return;

                // 转换 MB 到 Byte
                long limitBytes = Config.MaxFileSizeInMB * 1024L * 1024L;

                if (fileInfo.Length > limitBytes)
                {
                    // 1. 生成归档文件名：SqliteLogs_Backup_20231027_103001.db
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string archiveDbPath = Path.Combine(DirectoryPath, $"SqliteLogs_Backup_{timestamp}.db");

                    // 2. 重命名（移动）当前文件
                    // 注意：因为 WriteBatchToDb 里的 using 已经释放了连接，理论上文件未被锁定。
                    // 但为了保险，如果存在 .wal 或 .shm 文件，最好一起移走或忽略。
                    // 简单移动 .db 文件即可，WAL 机制下如果连接已关闭，WAL 文件通常会被合并或删除。
                    File.Move(SqliteDbPath, archiveDbPath);

                    // 3. 移动可能存在的 WAL 临时文件 (如果有的话，通常 Close 后就没了，但以防万一)
                    var walPath = SqliteDbPath + "-wal";
                    var shmPath = SqliteDbPath + "-shm";
                    if (File.Exists(walPath)) File.Delete(walPath);
                    if (File.Exists(shmPath)) File.Delete(shmPath);

                    // 4. 后台处理旧文件（压缩或删除），不阻塞当前的写入流程
                    Task.Run(() => ProcessRotatedFile(archiveDbPath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Rotate] Failed: {ex.Message}");
            }
        }

        private void ProcessRotatedFile(string filePath)
        {
            try
            {
                if (Config.IsCompressionEnabled)
                {
                    // === 启用压缩：压缩为 zip 后删除原 db ===
                    string zipPath = Path.ChangeExtension(filePath, ".zip");

                    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        // 将 db 文件放入压缩包，条目名为原文件名
                        archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                    }

                    // 压缩成功后删除原 db 文件
                    if (File.Exists(zipPath))
                    {
                        File.Delete(filePath);
                    }
                }
                else
                {
                    // === 未启用压缩：直接删除 ===
                    // 需求："不开超过大小就删除"
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessArchived] Failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Disable();
            Config.SqliteLogEnabledChanged -= OnSqliteLogEnabledChanged;
            GC.SuppressFinalize(this);
        }

        ~SqliteLogManager()
        {
            Dispose();
        }

        private static bool AppenderExists(Hierarchy hierarchy, IAppender appender)
        {
            foreach (var a in hierarchy.Root.Appenders)
            {
                if (a == appender) return true;
            }
            return false;
        }
    }
}