#pragma warning disable CS8622
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Messages
{

    public class MessagesListManager : ViewModelBase,IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MessagesListManager));

        private static MessagesListManager _instance;
        private static readonly object _locker = new();
        public static MessagesListManager GetInstance() { lock (_locker) { _instance ??= new MessagesListManager(); return _instance; } }

        public ObservableCollection<MsgRecord> MsgRecords { get; } = new ObservableCollection<MsgRecord>();

        private readonly RuntimeConfigOwner<MsgRecordManagerConfig> _configOwner;
        private readonly object _activeStateLocker = new();
        private ActiveDatabaseState _activeState;
        public MsgRecordManagerConfig Config => Volatile.Read(ref _activeState).Config;

        public RelayCommand EditConfigCommand { get; set; }

        public RelayCommand SelectDbFileCommand { get; set; }

        public RelayCommand MsgRecordsClearCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }
        public RelayCommand DeleteAllCommand { get; set; }
        public RelayCommand ResetDatabaseCommand { get; set; }
        public RelayCommand ReloadCommand { get; set; }

        public int TotalCount { get => _TotalCount; set { _TotalCount = value; OnPropertyChanged(); } }
        private int _TotalCount;

        public string FilterServiceName { get => _FilterServiceName; set { _FilterServiceName = value; OnPropertyChanged(); } }
        private string _FilterServiceName;

        public string FilterEventName { get => _FilterEventName; set { _FilterEventName = value; OnPropertyChanged(); } }
        private string _FilterEventName;

        public MsgRecordState? FilterMsgRecordState { get => _FilterMsgRecordState; set { _FilterMsgRecordState = value; OnPropertyChanged(); } }
        private MsgRecordState? _FilterMsgRecordState;

        /// <summary>
        /// 创建短生命周期的数据库连接
        /// </summary>
        private static SqlSugarClient CreateDb(string sqliteDbPath)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={sqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        public MessagesListManager()
            : this(
                () => ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>(),
                ConfigService.Instance as IConfigReloadNotifier)
        {
        }

        internal MessagesListManager(
            Func<MsgRecordManagerConfig> configFactory,
            IConfigReloadNotifier? reloadNotifier,
            bool registerDatabaseBrowser = true)
        {
            _configOwner = new RuntimeConfigOwner<MsgRecordManagerConfig>(
                configFactory,
                reloadNotifier,
                ex => log.Error("切换消息记录数据库失败，保留上一代运行态", ex));
            string initialDatabasePath = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(_configOwner.Current);
            _activeState = new ActiveDatabaseState(_configOwner.Current, initialDatabasePath, _configOwner.Generation);
            _configOwner.ConfigurationChanged += ConfigOwner_ConfigurationChanged;
            EditConfigCommand = new RelayCommand(_ => EditConfig());
            MsgRecordsClearCommand = new RelayCommand(_ => MsgRecords.Clear());
            GenericQueryCommand = new RelayCommand(_ => GenericQuery());
            QueryCommand = new RelayCommand(_ => QueryWithFilter());
            SelectDbFileCommand = new RelayCommand(_ => PlatformHelper.OpenFolderAndSelectFile(Config.SqliteDbPath));
            DeleteAllCommand = new RelayCommand(_ =>
            {
                if (MessageBox.Show(ColorVision.Engine.Properties.Resources.Engine_Msg_ConfirmDeleteAllRecords, ColorVision.Engine.Properties.Resources.Engine_Msg_ConfirmDeleteTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    DeleteAllRecords();
            });
            ResetDatabaseCommand = new RelayCommand(_ =>
            {
                if (MessageBox.Show(ColorVision.Engine.Properties.Resources.Engine_Msg_ConfirmResetDatabase, ColorVision.Engine.Properties.Resources.Engine_Msg_ConfirmResetTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    ResetDatabase();
            });
            ReloadCommand = new RelayCommand(_ => ReloadData());

            if (registerDatabaseBrowser)
            {
                DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
                    "sqlite.msgrecords",
                    ColorVision.Engine.Properties.Resources.Engine_Msg_MessageRecord,
                    CaptureDatabasePath,
                    dbPath => new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={dbPath}",
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute
                    })));
            }
        }

        public MsgRecordManagerConfig CaptureConfig()
        {
            return CaptureDatabaseSnapshot().Config;
        }

        internal string CaptureDatabasePath() => Volatile.Read(ref _activeState).DatabasePath;

        private DatabaseTaskSnapshot CaptureDatabaseSnapshot()
        {
            ActiveDatabaseState state = Volatile.Read(ref _activeState);
            return new DatabaseTaskSnapshot(_configOwner.CreateSnapshot(state.Config), state.DatabasePath, state.Generation);
        }

        private void ConfigOwner_ConfigurationChanged(object? sender, RuntimeConfigChangedEventArgs<MsgRecordManagerConfig> e)
        {
            string databasePath = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(e.Current);
            PreparedDatabaseView prepared = ReadDatabaseView(e.Current, databasePath, e.Current.Count);

            void CommitPreparedView()
            {
                lock (_activeStateLocker)
                {
                    if (e.Generation <= _activeState.Generation)
                        return;

                    MsgRecords.Clear();
                    foreach (MsgRecord row in prepared.Rows)
                        MsgRecords.Add(row);
                    TotalCount = prepared.TotalCount;
                    Volatile.Write(ref _activeState, new ActiveDatabaseState(e.Current, databasePath, e.Generation));
                }

                OnPropertyChanged(nameof(Config));
                OnPropertyChanged(nameof(MsgRecords));
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                CommitPreparedView();
            else
                dispatcher.Invoke(CommitPreparedView);
        }

        private bool _isListening;

        public void StartListening()
        {
            if (!_isListening)
            {
                MsgRecordDataBaseHelper.InsertedForDatabase += OnMsgRecordInserted;
                _isListening = true;
            }
        }

        public void StopListening()
        {
            if (_isListening)
            {
                MsgRecordDataBaseHelper.InsertedForDatabase -= OnMsgRecordInserted;
                _isListening = false;
            }
        }

        private void OnMsgRecordInserted(object? sender, MsgRecordInsertedEventArgs e)
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            if (!string.Equals(
                e.DatabasePath,
                snapshot.DatabasePath,
                StringComparison.OrdinalIgnoreCase))
                return;

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!string.Equals(
                    e.DatabasePath,
                    CaptureDatabasePath(),
                    StringComparison.OrdinalIgnoreCase))
                    return;

                if (snapshot.Config.OrderByType == OrderByType.Desc)
                    MsgRecords.Insert(0, e.Item);
                else
                    MsgRecords.Add(e.Item);
                TotalCount++;
            }));
        }

        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }

        private void RefreshTotalCount()
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            using var db = CreateDb(snapshot.DatabasePath);
            TotalCount = db.Queryable<MsgRecord>().Count();
        }

        /// <summary>
        /// 初始化，从数据库读取数据，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            LoadAll(snapshot.Config, snapshot.DatabasePath, count);
        }

        private void LoadAll(MsgRecordManagerConfig config, string databasePath, int count)
        {
            MsgRecords.Clear();
            using var db = CreateDb(databasePath);
            var query = db.Queryable<MsgRecord>().OrderBy(x => x.Id, config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                MsgRecords.Add(dbItem);
            }
            TotalCount = db.Queryable<MsgRecord>().Count();
        }

        /// <summary>
        /// 根据过滤条件查询
        /// </summary>
        public void QueryWithFilter()
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            MsgRecordManagerConfig config = snapshot.Config;
            MsgRecords.Clear();
            using var db = CreateDb(snapshot.DatabasePath);
            var query = db.Queryable<MsgRecord>();

            if (!string.IsNullOrWhiteSpace(FilterServiceName))
                query = query.Where(x => x.MsgSendJson.Contains(FilterServiceName));

            if (!string.IsNullOrWhiteSpace(FilterEventName))
                query = query.Where(x => x.MsgSendJson.Contains(FilterEventName));

            if (FilterMsgRecordState.HasValue)
                query = query.Where(x => x.MsgRecordState == FilterMsgRecordState.Value);

            query = query.OrderBy(x => x.Id, config.OrderByType);

            var dbList = config.Count > 0 ? query.Take(config.Count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                MsgRecords.Add(dbItem);
            }
            TotalCount = db.Queryable<MsgRecord>().Count();
        }

        /// <summary>
        /// 删除数据库中所有记录并清空列表
        /// </summary>
        public void DeleteAllRecords()
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            using (var db = CreateDb(snapshot.DatabasePath))
            {
                db.Deleteable<MsgRecord>().ExecuteCommand();
            }
            MsgRecords.Clear();
            TotalCount = 0;
        }

        /// <summary>
        /// 重置数据库（删除db文件并重新创建）
        /// </summary>
        public void ResetDatabase()
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            string databasePath = snapshot.DatabasePath;
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Force GC to release file handles held by disposed SQLite connections
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(databasePath))
                File.Delete(databasePath);
            using (var db = CreateDb(databasePath))
            {
                db.CodeFirst.InitTables<MsgRecord>();
            }
            MsgRecords.Clear();
            TotalCount = 0;
        }

        /// <summary>
        /// 重新加载数据
        /// </summary>
        public void ReloadData()
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            LoadAll(snapshot.Config, snapshot.DatabasePath, snapshot.Config.Count);
        }

        public void GenericQuery()
        {
            DatabaseTaskSnapshot snapshot = CaptureDatabaseSnapshot();
            var db = CreateDb(snapshot.DatabasePath);
            GenericQuery<MsgRecord> genericQuery = new GenericQuery<MsgRecord>(db, MsgRecords);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            try
            {
                genericQueryWindow.ShowDialog();
            }
            finally
            {
                db.Dispose();
            }
        }

        public void Dispose()
        {
            StopListening();
            _configOwner.ConfigurationChanged -= ConfigOwner_ConfigurationChanged;
            _configOwner.Dispose();
            GC.SuppressFinalize(this);
        }

        private static PreparedDatabaseView ReadDatabaseView(MsgRecordManagerConfig config, string databasePath, int count)
        {
            using var db = CreateDb(databasePath);
            var query = db.Queryable<MsgRecord>().OrderBy(x => x.Id, config.OrderByType);
            List<MsgRecord> rows = count > 0 ? query.Take(count).ToList() : query.ToList();
            return new PreparedDatabaseView(rows, db.Queryable<MsgRecord>().Count());
        }

        private sealed class ActiveDatabaseState
        {
            public ActiveDatabaseState(MsgRecordManagerConfig config, string databasePath, long generation)
            {
                Config = config;
                DatabasePath = databasePath;
                Generation = generation;
            }

            public MsgRecordManagerConfig Config { get; }
            public string DatabasePath { get; }
            public long Generation { get; }
        }

        private sealed class PreparedDatabaseView
        {
            public PreparedDatabaseView(List<MsgRecord> rows, int totalCount)
            {
                Rows = rows;
                TotalCount = totalCount;
            }

            public List<MsgRecord> Rows { get; }
            public int TotalCount { get; }
        }

        private sealed class DatabaseTaskSnapshot
        {
            public DatabaseTaskSnapshot(MsgRecordManagerConfig config, string databasePath, long generation)
            {
                Config = config;
                DatabasePath = databasePath;
                Generation = generation;
            }

            public MsgRecordManagerConfig Config { get; }
            public string DatabasePath { get; }
            public long Generation { get; }
        }


    }
}
