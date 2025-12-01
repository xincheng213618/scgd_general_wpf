#pragma warning disable CS8618
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket消息管理器配置
    /// </summary>
    public class SocketMessageManagerConfig : ViewModelBase, IConfig
    {
        [DisplayName("查询数量"), Category("View")]
        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count = 100;

        [DisplayName("按类型排序"), Category("View")]
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

        public ObservableCollection<SocketMessage> Messages { get; set; } = new ObservableCollection<SocketMessage>();

        public SocketMessageManagerConfig Config { get; set; }

        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand SelectDbFileCommand { get; set; }
        public RelayCommand MessagesClearCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }

        public SocketMessageManager()
        {
            Config = ConfigService.Instance.GetRequiredService<SocketMessageManagerConfig>();
            EditConfigCommand = new RelayCommand(_ => EditConfig());
            MessagesClearCommand = new RelayCommand(_ => Messages.Clear());
            GenericQueryCommand = new RelayCommand(_ => GenericQuery());
            QueryCommand = new RelayCommand(_ => LoadAll(Config.Count));
            SelectDbFileCommand = new RelayCommand(_ => PlatformHelper.OpenFolderAndSelectFile(SqliteDbPath));

            // 确保目录存在
            Directory.CreateDirectory(DirectoryPath);

            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            // 确保表存在
            _db.CodeFirst.InitTables<SocketMessage>();
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
            Messages.Clear();
            // 限制最大加载数量以避免内存问题
            int effectiveCount = count <= 0 ? Config.Count : Math.Min(count, 1000);
            var query = _db.Queryable<SocketMessage>().OrderBy(x => x.Id, Config.OrderByType);
            var dbList = query.Take(effectiveCount).ToList();
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
                int id = _db.Insertable(message).ExecuteReturnIdentity();
                message.Id = id;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Config.OrderByType == OrderByType.Desc)
                    {
                        Messages.Insert(0, message);
                    }
                    else
                    {
                        Messages.Add(message);
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error("Error adding socket message", ex);
            }
        }

        /// <summary>
        /// 删除消息
        /// </summary>
        public void DeleteMessage(SocketMessage message)
        {
            try
            {
                if (message == null) return;
                _db.Deleteable<SocketMessage>().Where(x => x.Id == message.Id).ExecuteCommand();
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
            GenericQuery<SocketMessage> genericQuery = new GenericQuery<SocketMessage>(_db, Messages);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            };
            genericQueryWindow.ShowDialog();
        }

        public void Dispose()
        {
            _db?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
