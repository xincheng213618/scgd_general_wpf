#pragma warning disable CS8618
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows;

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

            // 确保数据库所在目录存在；测试或诊断工具可以安全替换数据库路径。
            string databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(SqliteDbPath))
                ?? throw new InvalidOperationException("无法确定 Socket 消息数据库目录。");
            Directory.CreateDirectory(databaseDirectory);

            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
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

            public static IDatabaseBrowserProvider CreateBrowserProvider() =>
                new SqliteDatabaseBrowserProvider(
                    "sqlite.socketmessages",
                    Properties.Resources.Socket_MessageTable,
                    () => SqliteDbPath,
                    dbPath => new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={dbPath}",
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute
                    }));

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
                string? content = message.Content;
                message.ContentPreview = GzipTextPayloadCodec.CreatePreview(
                    content,
                    SocketMessagePayloadStorage.PreviewCharacters);

                int id = SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
                {
                    _db.Ado.BeginTran();
                    try
                    {
                        int insertedId = _db.Insertable(message).ExecuteReturnIdentity();
                        SocketMessagePayloadStorage.Save(_db, insertedId, content);
                        _db.Ado.CommitTran();
                        return insertedId;
                    }
                    catch
                    {
                        _db.Ado.RollbackTran();
                        throw;
                    }
                });
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
            GenericQuery<SocketMessage> genericQuery = new SocketMessageGenericQuery(_db, Messages);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner 
            };
            genericQueryWindow.ShowDialog();
        }

        public void Dispose()
        {
            SocketMessagePayloadStorage.RunDatabaseMaintenance(() => _db?.Dispose());
            GC.SuppressFinalize(this);
        }

        private sealed class SocketMessageGenericQuery : GenericQuery<SocketMessage>
        {
            public SocketMessageGenericQuery(SqlSugarClient db, IList<SocketMessage> viewResults)
                : base(db, viewResults)
            {
            }

            public override void QueryDB()
            {
                SocketMessagePayloadStorage.RunDatabaseMaintenance(base.QueryDB);
            }

            public override void DeleteAll()
            {
                SocketMessagePayloadStorage.RunDatabaseMaintenance(base.DeleteAll);
            }

            public override void TruncateTable()
            {
                SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
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
