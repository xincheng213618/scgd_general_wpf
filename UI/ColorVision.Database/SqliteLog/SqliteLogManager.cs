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
using System.IO.Compression; // 必须引用: System.IO.Compression.FileSystem
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

        // 固定当前正在使用的活跃数据库文件名
        // 轮转逻辑：始终往 SqliteLogs.db 写，写满了移走改名，再自动创建新的 SqliteLogs.db
        public static string SqliteDbPath { get; set; } = Path.Combine(DirectoryPath, "SqliteLogs.db");

        public static SqliteLogManagerConfig Config => ConfigService.Instance.GetRequiredService<SqliteLogManagerConfig>();
        public static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }
        // 异步队列
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

                // 初始化队列
                _logQueue = new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>());
                _cts = new CancellationTokenSource();

                // 注册 log4net appender
                _hierarchy = (Hierarchy)LogManager.GetRepository();
                if (!AppenderExists(_hierarchy, this))
                {
                    _hierarchy.Root.AddAppender(this);
                    _hierarchy.Configured = true;
                    _hierarchy.RaiseConfigurationChanged(EventArgs.Empty);
                }

                // 启动后台写入线程
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
                try
                {
                    _writeTask?.Wait(3000);
                }
                catch { /* 忽略取消异常 */ }

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

                        // 【新增逻辑】写入完成后，检查文件大小并执行轮转
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
                    System.Diagnostics.Debug.WriteLine($"SqliteLogManager Error: {ex.Message}");
                }
            }
        }

        private void WriteBatchToDb(List<LogEntry> logs)
        {
            if (logs.Count == 0) return;

            // 使用短连接模式，确保文件句柄在使用后立即释放，方便文件移动
            using (var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath};",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            }))
            {
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
                    // 这里可以记录错误，或者尝试写入到备用文本文件
                }

                // 关键点：在准备检查文件大小前，强制将 WAL 文件的内容 Checkpoint 回主数据库文件
                // 这样移动主 .db 文件时才不会丢失 WAL 中的数据
                // TRUNCATE 模式会清空 WAL 文件并将数据写入 DB，且保持 WAL 文件大小为 0
                try { db.Ado.ExecuteCommand("PRAGMA wal_checkpoint(TRUNCATE);"); } catch { }
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

                // 配置中的 MB 转为 Byte
                long limitBytes = Config.MaxFileSizeInMB * 1024L * 1024L;

                if (fileInfo.Length > limitBytes)
                {
                    // 1. 确定备份文件名 (例如: SqliteLogs_Backup_20231027_103001.db)
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string archiveDbPath = Path.Combine(DirectoryPath, $"SqliteLogs_Backup_{timestamp}.db");

                    // 2. 重命名（移动）当前文件
                    // 因为 WriteBatchToDb 里的 using 已经结束，且执行了 checkpoint，理论上文件未被锁定。
                    File.Move(SqliteDbPath, archiveDbPath);

                    // 3. 清理残留的 WAL/SHM 临时文件 (如果有)
                    string walPath = SqliteDbPath + "-wal";
                    string shmPath = SqliteDbPath + "-shm";
                    if (File.Exists(walPath)) File.Delete(walPath);
                    if (File.Exists(shmPath)) File.Delete(shmPath);

                    // 4. 启动后台任务处理旧文件（压缩或删除），不阻塞当前的写入流程
                    // 必须传参进去，因为 archiveDbPath 是局部变量
                    Task.Run(() => ProcessRotatedFile(archiveDbPath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Rotate] Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 后台处理归档文件
        /// </summary>
        private void ProcessRotatedFile(string filePath)
        {
            try
            {
                if (Config.IsCompressionEnabled)
                {
                    // === 启用压缩：压缩为 zip 后删除原 db ===
                    string zipPath = Path.ChangeExtension(filePath, ".zip");

                    // 检查 zip 是否已存在（极端情况），存在则不再覆盖
                    if (!File.Exists(zipPath))
                    {
                        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                        {
                            // 将 db 文件放入压缩包
                            archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                        }
                    }

                    // 压缩成功后，删除原始的大 db 文件
                    if (File.Exists(zipPath))
                    {
                        File.Delete(filePath);
                    }
                }
                else
                {
                    // === 未启用压缩：直接删除 ===
                    // 满足需求："不开压缩超过大小就删除"
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessRotatedFile] Failed: {ex.Message}");
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