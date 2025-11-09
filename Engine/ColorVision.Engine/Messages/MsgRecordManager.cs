using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Messages
{

    public class MsgRecordManagerConfig : ViewModelBase, IConfig
    {
        [DisplayName("QueryCount"), Category("View")]
        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count = 50;

        [DisplayName("SortByType"), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; OnPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;
    }

    public class MsgRecordManager : ViewModelBase,IDisposable
    {
        private readonly ILog log = LogManager.GetLogger(typeof(MsgRecordManager));

        private static MsgRecordManager _instance;
        private static readonly object _locker = new();
        public static MsgRecordManager GetInstance() { lock (_locker) { _instance ??= new MsgRecordManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        public static string SqliteDbPath { get; set; } = DirectoryPath + "MsgRecords.db";
        private readonly SqlSugarClient _db;
        public ObservableCollection<MsgRecord> MsgRecords { get; set; } = new ObservableCollection<MsgRecord>();
        public MsgRecordManagerConfig Config { get; set; }

        public RelayCommand EditConfigCommand { get; set; }

        public RelayCommand SelectDbFileCommand { get; set; }

        public RelayCommand MsgRecordsClearCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }

        public MsgRecordManager()
        {
            Config = ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
            EditConfigCommand = new RelayCommand(_ => EditConfig());
            MsgRecordsClearCommand = new RelayCommand(_ => MsgRecords.Clear());
            GenericQueryCommand = new RelayCommand(_ => GenericQuery());
            QueryCommand = new RelayCommand(_ => LoadAll(Config.Count));
            SelectDbFileCommand = new RelayCommand(_ => PlatformHelper.OpenFolderAndSelectFile(SqliteDbPath));

            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            // 确保表存在
            _db.CodeFirst.InitTables<MsgRecord>();
        }

        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }
        public void MsgRecordsClear()
        {
            MsgRecords.Clear();
        }

        public void Delete(int index)
        {
            MsgRecords.RemoveAt(index);
        }


        /// <summary>
        /// 初始化，从数据库读取数据到ViewResluts，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            MsgRecords.Clear();
            var query = _db.Queryable<MsgRecord>().OrderBy(x => x.Id, Config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                MsgRecords.Add(dbItem);
            }
        }

        public void Insertable(MsgRecord item)
        {
            try
            {
                if (item == null) return;
                int id = _db.Insertable(item).ExecuteReturnIdentity();
                item.Id = id; // 更新ID

                if (Config.OrderByType == OrderByType.Desc)
                {
                    MsgRecords.Insert(0, item); //倒序插入
                }
                else
                {
                    MsgRecords.Add(item);
                }

                item.MsgRecordStateChanged += (e) =>
                {
                    _db.Updateable(item).ExecuteCommand();
                };
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }


        }

        public void GenericQuery()
        {
            GenericQuery<MsgRecord> genericQuery = new GenericQuery<MsgRecord>(_db, MsgRecords);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }

        public void Dispose()
        {
            _db?.Dispose();
            GC.SuppressFinalize(this);
        }


    }
}
