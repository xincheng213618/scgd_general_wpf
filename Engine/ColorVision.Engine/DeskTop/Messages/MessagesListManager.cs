using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.UI;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.DeskTop.Messages
{

    public class MessagesListManager : ViewModelBase,IDisposable
    {

        private static MessagesListManager _instance;
        private static readonly object _locker = new();
        public static MessagesListManager GetInstance() { lock (_locker) { _instance ??= new MessagesListManager(); return _instance; } }

        public ObservableCollection<MsgRecord> MsgRecords { get; set; } = new ObservableCollection<MsgRecord>();

        public MsgRecordManagerConfig Config { get; set; }

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
        private SqlSugarClient CreateDb()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={Config.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        public MessagesListManager()
        {
            Config = ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
            EditConfigCommand = new RelayCommand(_ => EditConfig());
            MsgRecordsClearCommand = new RelayCommand(_ => MsgRecords.Clear());
            GenericQueryCommand = new RelayCommand(_ => GenericQuery());
            QueryCommand = new RelayCommand(_ => QueryWithFilter());
            SelectDbFileCommand = new RelayCommand(_ => PlatformHelper.OpenFolderAndSelectFile(Config.SqliteDbPath));
            DeleteAllCommand = new RelayCommand(_ =>
            {
                if (MessageBox.Show("确定要删除数据库中所有记录吗？此操作不可恢复。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    DeleteAllRecords();
            });
            ResetDatabaseCommand = new RelayCommand(_ =>
            {
                if (MessageBox.Show("确定要重置数据库吗？将删除数据库文件并重新创建。此操作不可恢复。", "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    ResetDatabase();
            });
            ReloadCommand = new RelayCommand(_ => ReloadData());

            using (var db = CreateDb())
            {
                db.CodeFirst.InitTables<MsgRecord>();
            }
        }

        private bool _isListening;

        public void StartListening()
        {
            if (!_isListening)
            {
                MsgRecordDataBaseHelper.Inserted += OnMsgRecordInserted;
                _isListening = true;
            }
        }

        public void StopListening()
        {
            if (_isListening)
            {
                MsgRecordDataBaseHelper.Inserted -= OnMsgRecordInserted;
                _isListening = false;
            }
        }

        private void OnMsgRecordInserted(object sender, MsgRecord item)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Config.OrderByType == OrderByType.Desc)
                    MsgRecords.Insert(0, item);
                else
                    MsgRecords.Add(item);
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
            using var db = CreateDb();
            TotalCount = db.Queryable<MsgRecord>().Count();
        }

        /// <summary>
        /// 初始化，从数据库读取数据，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            MsgRecords.Clear();
            using var db = CreateDb();
            var query = db.Queryable<MsgRecord>().OrderBy(x => x.Id, Config.OrderByType);
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
            MsgRecords.Clear();
            using var db = CreateDb();
            var query = db.Queryable<MsgRecord>();

            if (!string.IsNullOrWhiteSpace(FilterServiceName))
                query = query.Where(x => x.MsgSendJson.Contains(FilterServiceName));

            if (!string.IsNullOrWhiteSpace(FilterEventName))
                query = query.Where(x => x.MsgSendJson.Contains(FilterEventName));

            if (FilterMsgRecordState.HasValue)
                query = query.Where(x => x.MsgRecordState == FilterMsgRecordState.Value);

            query = query.OrderBy(x => x.Id, Config.OrderByType);

            var dbList = Config.Count > 0 ? query.Take(Config.Count).ToList() : query.ToList();
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
            using (var db = CreateDb())
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
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Force GC to release file handles held by disposed SQLite connections
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (File.Exists(Config.SqliteDbPath))
                File.Delete(Config.SqliteDbPath);
            using (var db = CreateDb())
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
            LoadAll(Config.Count);
        }

        public void GenericQuery()
        {
            var db = CreateDb();
            try
            {
                GenericQuery<MsgRecord> genericQuery = new GenericQuery<MsgRecord>(db, MsgRecords);
                GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                genericQueryWindow.Closed += (s, e) => db.Dispose();
                genericQueryWindow.ShowDialog();
            }
            catch
            {
                db.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            StopListening();
            GC.SuppressFinalize(this);
        }


    }
}
