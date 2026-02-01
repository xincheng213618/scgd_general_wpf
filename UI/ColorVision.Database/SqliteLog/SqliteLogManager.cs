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

        // 优化点4：动态获取数据库路径，支持按月分库，避免单文件过大
        public static string GetSqliteDbPath()
        {
            string month = DateTime.Now.ToString("yyyyMM");
            return Path.Combine(DirectoryPath, $"SqliteLogs_{month}.db");
        }

        public static SqliteLogManagerConfig Config => ConfigService.Instance.GetRequiredService<SqliteLogManagerConfig>();

        // 优化点1：使用 BlockingCollection 实现高效的生产者-消费者模型
        private BlockingCollection<LogEntry> _logQueue;
        private CancellationTokenSource _cts;
        private Task _writeTask;
        private bool _isEnabled = false;

        private const int BatchSize = 200; // 适当增大批量
        private const int FlushIntervalMs = 2000; // 2秒超时刷盘

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

                // 停止写入线程并等待完成
                _logQueue?.CompleteAdding();
                try
                {
                    _writeTask?.Wait(3000); // 等待最多3秒让剩余日志写完
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

            // 优化点1关键：只入队，绝不在此处 Flush，保证主线程/UI线程极速返回
            _logQueue.TryAdd(entry);
        }

        // 后台消费者线程方法
        private void ProcessQueue()
        {
            // 在线程内部创建连接，避免多线程共用连接实例的问题
            // 注意：这里需要根据当前时间动态检查是否需要切换数据库文件

            var buffer = new List<LogEntry>(BatchSize);

            while (!_cts.Token.IsCancellationRequested && !_logQueue.IsCompleted)
            {
                try
                {
                    LogEntry entry;
                    // 尝试从队列取数据，带超时机制
                    if (_logQueue.TryTake(out entry, FlushIntervalMs, _cts.Token))
                    {
                        buffer.Add(entry);
                    }

                    // 满足条件则写入：缓冲区满 OR 队列已空(LogQueue.Count == 0 且 buffer有数据) OR 超时导致TryTake返回false但buffer有数据
                    if (buffer.Count >= BatchSize || (buffer.Count > 0 && _logQueue.Count == 0))
                    {
                        WriteBatchToDb(buffer);
                        buffer.Clear();
                    }
                }
                catch (OperationCanceledException)
                {
                    // 即使取消了，如果buffer里有残留数据，最好也写进去
                    if (buffer.Count > 0) WriteBatchToDb(buffer);
                    break;
                }
                catch (Exception ex)
                {
                    // 避免日志系统自身崩溃导致程序退出，可以将错误输出到控制台或Debug
                    System.Diagnostics.Debug.WriteLine($"SqliteLogManager Error: {ex.Message}");
                }
            }
        }

        private void WriteBatchToDb(List<LogEntry> logs)
        {
            if (logs.Count == 0) return;

            // 每次写入获取当前月份的数据库路径，实现自动按月分表/分库
            string currentDbPath = GetSqliteDbPath();

            using (var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={currentDbPath};", // Cache Size 优化
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            }))
            {
                // 优化点2：开启 WAL 模式和 Normal 同步（极大提升写入性能）
                // 只需要在连接首次建立或偶尔执行即可，但在每次打开连接后执行 pragma 确保生效开销很小
                db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
                db.Ado.ExecuteCommand("PRAGMA synchronous=NORMAL;");

                // 确保表存在 (SqlSugar 会自动缓存表结构检查，性能损耗不大，或者可以由外部统一初始化)
                db.CodeFirst.InitTables<LogEntry>();

                // 优化点3：使用事务批量插入
                try
                {
                    db.BeginTran();
                    db.Insertable(logs).ExecuteCommand();
                    db.CommitTran();
                }
                catch
                {
                    db.RollbackTran();
                    throw;
                }
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