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
using System.Threading.Channels;
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
        private const int MaximumDisplayedMessages = 1000;
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketMessageManager));
        private static SocketMessageManager? _instance;
        private static readonly object _locker = new();
        private static int _shutdownStarted;

        public static SocketMessageManager GetInstance()
        {
            lock (_locker)
            {
                if (Volatile.Read(ref _shutdownStarted) != 0)
                    throw new InvalidOperationException("Socket message recording has stopped for application shutdown.");
                _instance ??= new SocketMessageManager();
                if (Volatile.Read(ref _shutdownStarted) != 0)
                {
                    _instance.Shutdown(TimeSpan.Zero);
                    throw new InvalidOperationException("Socket message recording has stopped for application shutdown.");
                }
                return _instance;
            }
        }

        internal static bool ShutdownExisting(TimeSpan timeout)
        {
            var deadline = SocketShutdownDeadline.Start(timeout);
            Interlocked.Exchange(ref _shutdownStarted, 1);
            if (!Monitor.TryEnter(_locker, deadline.Remaining)) return false;
            SocketMessageManager? manager;
            try { manager = _instance; }
            finally { Monitor.Exit(_locker); }
            return manager == null || manager.Shutdown(deadline.Remaining);
        }

        public static string DirectoryPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ColorVision", "Config");
        
        public static string SqliteDbPath { get; set; } = Path.Combine(DirectoryPath, "SocketMessages.db");

        private readonly SqlSugarClient _db;
        private readonly Dispatcher? _dispatcher;
        private readonly object _queueGate = new();
        private readonly Channel<Func<Task>> _writeQueue = Channel.CreateUnbounded<Func<Task>>(new()
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });
        private readonly Task _writeWorker;
        private long _recordSequence;
        private long _displaySuppressedThrough;
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
            _writeWorker = Task.Run(ProcessWriteQueueAsync);
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
            int effectiveCount = Math.Clamp(count <= 0 ? Config.Count : count, 1, MaximumDisplayedMessages);
            List<SocketMessage> dbList = new();
            RunAfterPendingMessages(() =>
            {
                var query = _db.Queryable<SocketMessage>().OrderBy(x => x.Id, Config.OrderByType);
                dbList = query.Take(effectiveCount).ToList();
            });

            Messages.Clear();
            foreach (var item in dbList)
            {
                Messages.Add(item);
            }
        }

        /// <summary>
        /// 保存消息快照并入队；协议收发不等待数据库或 UI。
        /// </summary>
        public void AddMessage(SocketMessage message)
        {
            try
            {
                if (message == null) return;
                var snapshot = new SocketMessage
                {
                    ClientEndPoint = message.ClientEndPoint,
                    Direction = message.Direction,
                    Content = message.Content,
                    MessageTime = message.MessageTime,
                    EventName = message.EventName,
                    MsgID = message.MsgID,
                    ResponseCode = message.ResponseCode,
                };
                long queuedAt = Stopwatch.GetTimestamp();
                lock (_queueGate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(SocketMessageManager));
                    snapshot.RecordSequence = message.RecordSequence = ++_recordSequence;
                    _writeQueue.Writer.TryWrite(() =>
                    {
                        PersistMessage(snapshot, queuedAt);
                        return Task.CompletedTask;
                    });
                }
            }
            catch (Exception ex)
            {
                log.Error("Error queueing socket message", ex);
            }
        }

        private void PersistMessage(SocketMessage message, long queuedAt)
        {
            try
            {
                double queueMs = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds;
                Stopwatch timing = Stopwatch.StartNew();
                string? content = message.Content;
                message.ContentPreview = GzipTextPayloadCodec.CreatePreview(content, SocketMessagePayloadStorage.PreviewCharacters);

                double gateRequestedAt = timing.Elapsed.TotalMilliseconds;
                double gateEnteredAt = 0;
                double committedAt = 0;
                double transactionStartedAt = 0;
                double insertedAt = 0;
                double payloadSavedAt = 0;
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                {
                    gateEnteredAt = timing.Elapsed.TotalMilliseconds;
                    _db.Ado.BeginTran();
                    transactionStartedAt = timing.Elapsed.TotalMilliseconds;
                    int insertedId;
                    try
                    {
                        insertedId = _db.Insertable(message).ExecuteReturnIdentity();
                        insertedAt = timing.Elapsed.TotalMilliseconds;
                        SocketMessagePayloadStorage.Save(_db, insertedId, content);
                        payloadSavedAt = timing.Elapsed.TotalMilliseconds;
                        _db.Ado.CommitTran();
                    }
                    catch
                    {
                        _db.Ado.RollbackTran();
                        throw;
                    }
                    committedAt = timing.Elapsed.TotalMilliseconds;
                    message.Id = insertedId;
                    message.UnloadContent();
                    QueueMessageForDisplay(message);
                });
                log.Info(JsonConvert.SerializeObject(new
                {
                    Event = "SocketMessageTiming",
                    message.RecordSequence,
                    MessageId = message.Id,
                    message.EventName,
                    message.MsgID,
                    message.Direction,
                    message.MessageTime,
                    PersistenceQueueMs = Math.Round(queueMs, 3),
                    StorageGateWaitMs = Math.Round(gateEnteredAt - gateRequestedAt, 3),
                    StorageWriteMs = Math.Round(committedAt - gateEnteredAt, 3),
                    BeginTransactionMs = Math.Round(transactionStartedAt - gateEnteredAt, 3),
                    InsertMs = Math.Round(insertedAt - transactionStartedAt, 3),
                    PayloadSaveMs = Math.Round(payloadSavedAt - insertedAt, 3),
                    CommitMs = Math.Round(committedAt - payloadSavedAt, 3),
                    PersistMs = Math.Round(committedAt, 3),
                    UiUpdateAwaited = false,
                    PersistenceAwaited = false,
                }));
            }
            catch (Exception ex)
            {
                log.Error($"Error persisting socket message RecordSequence={message.RecordSequence}", ex);
            }
        }

        private async Task ProcessWriteQueueAsync()
        {
            try
            {
                await foreach (Func<Task> work in _writeQueue.Reader.ReadAllAsync().ConfigureAwait(false))
                    await work().ConfigureAwait(false);
            }
            finally
            {
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() => _db.Dispose());
            }
        }

        // Queries edit WPF collections on their calling thread. Pause only the writer,
        // after earlier messages commit; protocol producers can keep enqueueing.
        private void RunAfterPendingMessages(Action action)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            long boundary;
            lock (_queueGate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SocketMessageManager));
                boundary = _recordSequence;
                _writeQueue.Writer.TryWrite(async () =>
                {
                    ready.TrySetResult();
                    await resume.Task.ConfigureAwait(false);
                });
            }
            ready.Task.GetAwaiter().GetResult();
            try
            {
                lock (_queueGate)
                    Interlocked.Exchange(ref _displaySuppressedThrough, Math.Max(boundary, _displaySuppressedThrough));
                SocketMessagePayloadStorage.RunDatabaseMaintenance(action);
            }
            finally { resume.TrySetResult(); }
        }

        private void ClearMessages()
        {
            lock (_queueGate) Interlocked.Exchange(ref _displaySuppressedThrough, _recordSequence);
            Messages.Clear();
        }

        private void QueueMessageForDisplay(SocketMessage message)
        {
            Dispatcher? dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return; // The committed record is still available on the next query/startup.

            long queuedAt = Stopwatch.GetTimestamp();
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_disposed || message.RecordSequence <= Interlocked.Read(ref _displaySuppressedThrough))
                    return;
                double queueMs = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds;
                Stopwatch updateTiming = Stopwatch.StartNew();
                try
                {
                    // Only retire displayed rows; persisted history remains queryable.
                    while (Messages.Count >= MaximumDisplayedMessages)
                        Messages.RemoveAt(Config.OrderByType == OrderByType.Desc ? Messages.Count - 1 : 0);
                    if (Config.OrderByType == OrderByType.Desc)
                        Messages.Insert(0, message);
                    else
                        Messages.Add(message);

                    double updateMs = updateTiming.Elapsed.TotalMilliseconds;
                    bool slowUpdate = queueMs >= 1000 || updateMs >= 100;
                    // Normal projection has no new protocol information; keep slow UI evidence at INFO.
                    if (slowUpdate || log.IsDebugEnabled)
                    {
                        string timingJson = JsonConvert.SerializeObject(new
                        {
                            Event = "SocketMessageUiTiming",
                            message.RecordSequence,
                            MessageId = message.Id,
                            message.EventName,
                            message.MsgID,
                            UiQueueMs = Math.Round(queueMs, 3),
                            UiUpdateMs = Math.Round(updateMs, 3),
                            UiUpdateAwaited = false,
                        });
                        if (slowUpdate) log.Info(timingJson);
                        else log.Debug(timingJson);
                    }
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
            GenericQuery<SocketMessage> genericQuery = new SocketMessageGenericQuery(_db, Messages, RunAfterPendingMessages);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            };
            genericQueryWindow.ShowDialog();
        }

        public void Dispose()
        {
            Shutdown(Timeout.InfiniteTimeSpan);
            GC.SuppressFinalize(this);
        }

        internal bool Shutdown(TimeSpan timeout)
        {
            lock (_queueGate)
            {
                _disposed = true;
                _writeQueue.Writer.TryComplete();
            }
            bool completed = _writeWorker.Wait(timeout);
            if (!completed) log.Warn("Socket message persistence did not drain within the shutdown budget.");
            return completed;
        }

        private sealed class SocketMessageGenericQuery : GenericQuery<SocketMessage>
        {
            private readonly Action<Action> _runAfterPendingMessages;

            public SocketMessageGenericQuery(SqlSugarClient db, IList<SocketMessage> viewResults, Action<Action> runAfterPendingMessages)
                : base(db, viewResults)
            {
                _runAfterPendingMessages = runAfterPendingMessages;
            }

            public override void QueryDB()
            {
                _runAfterPendingMessages(base.QueryDB);
            }

            public override void DeleteAll()
            {
                _runAfterPendingMessages(base.DeleteAll);
            }

            public override void TruncateTable()
            {
                _runAfterPendingMessages(() =>
                {
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
