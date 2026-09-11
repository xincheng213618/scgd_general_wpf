#pragma warning disable CS8618
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket消息管理器配置
    /// </summary>
    public class SocketMessageManagerConfig : ViewModelBase, IConfig
    {
        [Display(Name = "Socket_QueryCount", ResourceType = typeof(Properties.Resources)), Category("View")]
        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count = 100;

        [Display(Name = "Socket_SortByType", ResourceType = typeof(Properties.Resources)), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; OnPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;
    }

    /// <summary>
    /// Socket消息管理器，负责消息的持久化和查询
    /// </summary>
    public class SocketMessageManager : ViewModelBase, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketMessageManager));
        private static SocketMessageManager? _instance;
        private static readonly object _locker = new();

        public static SocketMessageManager GetInstance()
        {
            lock (_locker)
            {
                return _instance ??= new SocketMessageManager();
            }
        }

        public static string DirectoryPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ColorVision", "Config");
        
        public static string SqliteDbPath { get; set; } = Path.Combine(DirectoryPath, "SocketMessages.db");

        private readonly SqlSugarClient _db;
        private readonly Dispatcher? _dispatcher;
        private long _displayGeneration;
        private volatile bool _disposed;

        public ObservableCollection<SocketMessage> Messages { get; set; } = new ObservableCollection<SocketMessage>();

        public SocketMessageManagerConfig Config { get; set; }

        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand SelectDbFileCommand { get; set; }
        public RelayCommand MessagesClearCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }

        public SocketMessageManager()
            : this(SqliteDbPath, ConfigService.Instance.GetRequiredService<SocketMessageManagerConfig>(), Application.Current?.Dispatcher)
        {
        }

        internal SocketMessageManager(string databasePath, SocketMessageManagerConfig config, Dispatcher? dispatcher)
        {
            Config = config;
            _dispatcher = dispatcher;
            EditConfigCommand = new RelayCommand(_ => EditConfig());
            MessagesClearCommand = new RelayCommand(_ => ClearMessages());
            GenericQueryCommand = new RelayCommand(_ => GenericQuery());
            QueryCommand = new RelayCommand(_ => LoadAll(Config.Count));
            SelectDbFileCommand = new RelayCommand(_ => PlatformHelper.OpenFolderAndSelectFile(databasePath));

            // 确保数据库所在目录存在；测试或诊断工具可以安全替换数据库路径。
            string databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(databasePath))
                ?? throw new InvalidOperationException("无法确定 Socket 消息数据库目录。");
            Directory.CreateDirectory(databaseDirectory);

            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={databasePath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            // 建表和补列也必须与历史迁移串行，避免两边同时修改 SQLite schema。
            SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
            {
                _db.CodeFirst.InitTables<SocketMessage>();
                SocketMessagePayloadStorage.EnsureSchema(_db);
            });
        }

        public void EditConfig()
        {
            new PropertyEditorWindow(Config) 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }

        /// <summary>
        /// 从数据库加载消息记录
        /// </summary>
        /// <param name="count">要加载的记录数，默认100条，最大1000条</param>
        public void LoadAll(int count = 100)
        {
            // 限制最大加载数量以避免内存问题
            int effectiveCount = count <= 0 ? Config.Count : Math.Min(count, 1000);
            List<SocketMessage> dbList = SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
            {
                // Queued rows already included in this query must not be inserted twice.
                InvalidatePendingDisplay();
                var query = _db.Queryable<SocketMessage>().OrderBy(x => x.Id, Config.OrderByType);
                return query.Take(effectiveCount).ToList();
            });

            Messages.Clear();
            foreach (var item in dbList)
            {
                Messages.Add(item);
            }
        }

        /// <summary>
        /// 添加新消息并持久化
        /// </summary>
        public void AddMessage(SocketMessage message)
        {
            try
            {
                if (message == null) return;
                Stopwatch timing = Stopwatch.StartNew();
                string? content = message.Content;
                message.ContentPreview = GzipTextPayloadCodec.CreatePreview(
                    content,
                    SocketMessagePayloadStorage.PreviewCharacters);

                double gateRequestedAt = timing.Elapsed.TotalMilliseconds;
                double gateEnteredAt = 0;
                double committedAt = 0;
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(SocketMessageManager));
                    gateEnteredAt = timing.Elapsed.TotalMilliseconds;
                    _db.Ado.BeginTran();
                    int insertedId;
                    try
                    {
                        insertedId = _db.Insertable(message).ExecuteReturnIdentity();
                        SocketMessagePayloadStorage.Save(_db, insertedId, content);
                        _db.Ado.CommitTran();
                    }
                    catch
                    {
                        _db.Ado.RollbackTran();
                        throw;
                    }
                    committedAt = timing.Elapsed.TotalMilliseconds;
                    message.Id = insertedId;
                    // Posting under the storage gate keeps display order equal to commit order.
                    // Never wait for the UI while holding this gate (or before dispatching a request).
                    QueueMessageForDisplay(message, Interlocked.Read(ref _displayGeneration));
                });
                log.Info(JsonConvert.SerializeObject(new
                {
                    Event = "SocketMessageTiming",
                    MessageId = message.Id,
                    message.EventName,
                    message.MsgID,
                    message.Direction,
                    message.MessageTime,
                    StorageGateWaitMs = Math.Round(gateEnteredAt - gateRequestedAt, 3),
                    StorageWriteMs = Math.Round(committedAt - gateEnteredAt, 3),
                    PersistMs = Math.Round(committedAt, 3),
                    UiUpdateAwaited = false,
                }));
            }
            catch (Exception ex)
            {
                log.Error("Error adding socket message", ex);
            }
        }

        private void InvalidatePendingDisplay() => Interlocked.Increment(ref _displayGeneration);

        private void ClearMessages()
        {
            SocketMessagePayloadStorage.RunDatabaseMaintenance(InvalidatePendingDisplay);
            Messages.Clear();
        }

        private void QueueMessageForDisplay(SocketMessage message, long generation)
        {
            Dispatcher? dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return; // The committed record is still available on the next query/startup.

            long queuedAt = Stopwatch.GetTimestamp();
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_disposed || generation != Interlocked.Read(ref _displayGeneration))
                    return;
                double queueMs = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds;
                Stopwatch updateTiming = Stopwatch.StartNew();
                try
                {
                    if (Config.OrderByType == OrderByType.Desc)
                        Messages.Insert(0, message);
                    else
                        Messages.Add(message);

                    log.Info(JsonConvert.SerializeObject(new
                    {
                        Event = "SocketMessageUiTiming",
                        MessageId = message.Id,
                        message.EventName,
                        message.MsgID,
                        UiQueueMs = Math.Round(queueMs, 3),
                        UiUpdateMs = Math.Round(updateTiming.Elapsed.TotalMilliseconds, 3),
                        UiUpdateAwaited = false,
                    }));
                }
                catch (Exception ex)
                {
                    log.Error("Error publishing socket message to UI", ex);
                }
            }));
        }

        /// <summary>
        /// 按 Id 加载一条消息的正文。已加载内容会留在当前行对象中，避免重复查询和解压。
        /// </summary>
        public string? LoadContent(SocketMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (message.IsContentLoaded)
                return message.Content;

            string? content = SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                SocketMessagePayloadStorage.Load(_db, message.Id));
            message.Content = content;
            return content;
        }

        /// <summary>
        /// 删除消息
        /// </summary>
        public void DeleteMessage(SocketMessage message)
        {
            try
            {
                if (message == null) return;
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                    _db.Deleteable<SocketMessage>().Where(x => x.Id == message.Id).ExecuteCommand());
                Messages.Remove(message);
            }
            catch (Exception ex)
            {
                log.Error("Error deleting socket message", ex);
            }
        }

        /// <summary>
        /// 打开通用查询窗口
        /// </summary>
        public void GenericQuery()
        {
            GenericQuery<SocketMessage> genericQuery = new SocketMessageGenericQuery(_db, Messages, InvalidatePendingDisplay);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            };
            genericQueryWindow.ShowDialog();
        }

        public void Dispose()
        {
            SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
            {
                if (_disposed) return;
                _disposed = true;
                InvalidatePendingDisplay();
                _db?.Dispose();
            });
            GC.SuppressFinalize(this);
        }

        private sealed class SocketMessageGenericQuery : GenericQuery<SocketMessage>
        {
            private readonly Action _invalidatePendingDisplay;

            public SocketMessageGenericQuery(SqlSugarClient db, IList<SocketMessage> viewResults, Action invalidatePendingDisplay)
                : base(db, viewResults)
            {
                _invalidatePendingDisplay = invalidatePendingDisplay;
            }

            public override void QueryDB()
            {
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                {
                    _invalidatePendingDisplay();
                    base.QueryDB();
                });
            }

            public override void DeleteAll()
            {
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                {
                    _invalidatePendingDisplay();
                    base.DeleteAll();
                });
            }

            public override void TruncateTable()
            {
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                {
                    _invalidatePendingDisplay();
                    string tableName = Db.EntityMaintenance.GetTableName<SocketMessage>();
                    Db.Ado.BeginTran();
                    try
                    {
                        Db.Deleteable<SocketMessage>().ExecuteCommand();
                        Db.Ado.ExecuteCommand(
                            "DELETE FROM sqlite_sequence WHERE name = @tableName",
                            new SugarParameter("@tableName", tableName));
                        Db.Ado.CommitTran();
                        log.InfoFormat("Truncate SQLite table {0}", tableName);
                    }
                    catch
                    {
                        Db.Ado.RollbackTran();
                        throw;
                    }
                });
            }
        }
    }
}
