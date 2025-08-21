using ColorVision.Database.SqliteLog;
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
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Log\\";
        public static string SqliteDbPath { get; set; } = DirectoryPath + "Logs.db";

        public static SqliteLogManagerConfig Config => ConfigService.Instance.GetRequiredService<SqliteLogManagerConfig>();

        private SqlSugarClient _db;
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private Timer _timer;
        private bool _isEnabled = false;

        private const int BatchSize = 100;
        Hierarchy _hierarchy;
        public SqliteLogManager()
        {
            // 注册启停事件监听
            Config.SqliteLogEnabledChanged += OnSqliteLogEnabledChanged;

            // 根据当前配置初始化
            if (Config.IsEnabled)
            {
                Enable();
            }
        }
        private void OnSqliteLogEnabledChanged(object? sender, bool enabled)
        {
            if (enabled)
                Enable();
            else
                Disable();
        }

        private void Enable()
        {
            if (_isEnabled) return;
            _isEnabled = true;

            _hierarchy = (Hierarchy)LogManager.GetRepository();
            if (!AppenderExists(_hierarchy, this))
            {
                _hierarchy.Root.AddAppender(this);
                log4net.Config.BasicConfigurator.Configure(_hierarchy);
            }

            Directory.CreateDirectory(DirectoryPath);
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            _db.CodeFirst.InitTables<LogEntry>();

            _timer = new Timer(_ => Flush(), null, 2000, 2000);
        }

        private void Disable()
        {
            if (!_isEnabled) return;
            _isEnabled = false;

            _hierarchy?.Root.RemoveAppender(this);
            log4net.Config.BasicConfigurator.Configure(_hierarchy);

            _timer?.Dispose();
            Flush();
            _db?.Dispose();
            _db = null;
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var entry = new LogEntry
            {
                Date = loggingEvent.TimeStamp,
                Thread = loggingEvent.ThreadName ?? string.Empty,
                Level = loggingEvent.Level?.Name ?? string.Empty,
                Logger = loggingEvent.LoggerName ?? string.Empty,
                Message = loggingEvent.RenderedMessage ?? string.Empty,
                Exception = loggingEvent.ExceptionObject?.ToString() ??string.Empty,
            };
            _entries.Enqueue(entry);

            // 新增：达到批量阈值立即插入
            if (_entries.Count >= BatchSize)
            {
                Flush();
            }
        }

        // 批量插入
        private void Flush()
        {
            try
            {
                var list = new List<LogEntry>();
                while (_entries.TryDequeue(out var entry))
                {
                    list.Add(entry);
                    if (list.Count >= BatchSize)
                    {
                        // 分批插入，避免一次过多
                        _db.Insertable(list).ExecuteCommand();
                        list.Clear();
                    }
                }
                if (list.Count > 0)
                {
                    _db.Insertable(list).ExecuteCommand();
                }
            }
            catch
            {
                // 可以写入本地文件作为降级
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

        // 判断 Appender 是否已存在，防止重复添加
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
