using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.DeskTop.Messages
{

    public class MessagesListManager : ViewModelBase,IDisposable
    {

        private static MessagesListManager _instance;
        private static readonly object _locker = new();
        public static MessagesListManager GetInstance() { lock (_locker) { _instance ??= new MessagesListManager(); return _instance; } }

        private readonly SqlSugarClient _db;

        public ObservableCollection<MsgRecord> MsgRecords { get; set; } = new ObservableCollection<MsgRecord>();

        public MsgRecordManagerConfig Config { get; set; }

        public RelayCommand EditConfigCommand { get; set; }

        public RelayCommand SelectDbFileCommand { get; set; }

        public RelayCommand MsgRecordsClearCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }

        public MessagesListManager()
        {
            Config = ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
            EditConfigCommand = new RelayCommand(_ => EditConfig());
            MsgRecordsClearCommand = new RelayCommand(_ => MsgRecords.Clear());
            GenericQueryCommand = new RelayCommand(_ => GenericQuery());
            QueryCommand = new RelayCommand(_ => LoadAll(Config.Count));
            SelectDbFileCommand = new RelayCommand(_ => PlatformHelper.OpenFolderAndSelectFile(Config.SqliteDbPath));

            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={Config.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            _db.CodeFirst.InitTables<MsgRecord>();

        }

        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
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
