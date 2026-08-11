#pragma warning disable CS8622
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Messages
{

    public class MessagesListManager : ViewModelBase,IDisposable
    {

        private static MessagesListManager _instance;
        private static readonly object _locker = new();
        public static MessagesListManager GetInstance() { lock (_locker) { _instance ??= new MessagesListManager(); return _instance; } }

        public ObservableCollection<MsgRecord> MsgRecords { get; set; } = new ObservableCollection<MsgRecord>();

        private readonly RuntimeConfigOwner<MsgRecordManagerConfig> _configOwner;
        public MsgRecordManagerConfig Config => _configOwner.Current;

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
            _configOwner = new RuntimeConfigOwner<MsgRecordManagerConfig>(configFactory, reloadNotifier);
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

            MsgRecordDataBaseHelper.EnsureDatabaseInitialized(Config);
            if (registerDatabaseBrowser)
            {
                DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
                    "sqlite.msgrecords",
                    ColorVision.Engine.Properties.Resources.Engine_Msg_MessageRecord,
                    () => MsgRecordDataBaseHelper.NormalizeDatabasePath(Config.SqliteDbPath),
                    dbPath => new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={dbPath}",
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute
                    })));
            }
        }

        public MsgRecordManagerConfig CaptureConfig() => _configOwner.Capture();

        private void ConfigOwner_ConfigurationChanged(object? sender, RuntimeConfigChangedEventArgs<MsgRecordManagerConfig> e)
        {
            try
            {
                MsgRecordDataBaseHelper.EnsureDatabaseInitialized(e.Current);
                OnPropertyChanged(nameof(Config));
                ReloadData();
            }
            catch
            {
                // Config reload is shared by many participants. Keep this manager's
                // previous visible data if the new database cannot be opened.
            }
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
            MsgRecordManagerConfig config = CaptureConfig();
            if (!string.Equals(
                e.DatabasePath,
                MsgRecordDataBaseHelper.NormalizeDatabasePath(config.SqliteDbPath),
                StringComparison.OrdinalIgnoreCase))
                return;

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!string.Equals(
                    e.DatabasePath,
                    MsgRecordDataBaseHelper.NormalizeDatabasePath(Config.SqliteDbPath),
                    StringComparison.OrdinalIgnoreCase))
                    return;

                if (config.OrderByType == OrderByType.Desc)
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
            MsgRecordManagerConfig config = CaptureConfig();
            string databasePath = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(config);
            using var db = CreateDb(databasePath);
            TotalCount = db.Queryable<MsgRecord>().Count();
        }

        /// <summary>
        /// 初始化，从数据库读取数据，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            MsgRecordManagerConfig config = CaptureConfig();
            LoadAll(config, count);
        }

        private void LoadAll(MsgRecordManagerConfig config, int count)
        {
            string databasePath = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(config);
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
            MsgRecordManagerConfig config = CaptureConfig();
            string databasePath = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(config);
            MsgRecords.Clear();
            using var db = CreateDb(databasePath);
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
            MsgRecordManagerConfig config = CaptureConfig();
            string databasePath = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(config);
            using (var db = CreateDb(databasePath))
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
            MsgRecordManagerConfig config = CaptureConfig();
            string databasePath = MsgRecordDataBaseHelper.NormalizeDatabasePath(config.SqliteDbPath);
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
            MsgRecordManagerConfig config = CaptureConfig();
            LoadAll(config, config.Count);
        }

        public void GenericQuery()
        {
            MsgRecordManagerConfig config = CaptureConfig();
            string databasePath = MsgRecordDataBaseHelper.EnsureDatabaseInitialized(config);
            var db = CreateDb(databasePath);
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


    }
}
