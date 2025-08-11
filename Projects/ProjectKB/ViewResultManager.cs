using ColorVision.Common.MVVM;
using ColorVision.UI;
using SqlSugar;
using System.Collections.ObjectModel;

namespace ProjectKB
{
    public class ViewResultManagerConfig : ViewModelBase, IConfig
    {
        public int Count { get => _Count; set { _Count = value; NotifyPropertyChanged(); } }
        private int _Count = -1;
    }

    public class ViewResultManager : IDisposable
    {
        private static ViewResultManager _instance;
        private static readonly object _locker = new();
        public static ViewResultManager GetInstance() { lock (_locker) { _instance ??= new ViewResultManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string SqliteDbPath { get; set; } = DirectoryPath + "ProjectKB.db";

        public static ViewResultManagerConfig Config => ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>();

        public ObservableCollection<KBItemMaster> ViewResluts { get; set; } = new ObservableCollection<KBItemMaster>();

        private readonly SqlSugarClient _db;

        public ViewResultManager()
        {
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            // 确保表存在
            _db.CodeFirst.InitTables<KBItemMaster>();
            LoadAll(Config.Count);
        }

        /// <summary>
        /// 初始化，从数据库读取数据到ViewResluts，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            ViewResluts.Clear();
            var query = _db.Queryable<KBItemMaster>().OrderBy(x => x.Id, OrderByType.Desc);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public void Save(KBItemMaster item)
        {
            if (item == null) return;
            int id = _db.Insertable(item).ExecuteReturnIdentity();
            item.Id = id; // 更新ID
            ViewResluts.Insert(0, item); //倒序插入
        }

        /// <summary>
        /// 根据条件查询，举例：根据SN或Model等
        /// </summary>
        public void Query(string model = null, string sn = null)
        {
            ViewResluts.Clear();
            var query = _db.Queryable<KBItemMaster>();

            if (!string.IsNullOrWhiteSpace(model))
                query = query.Where(x => x.Model.Contains(model));
            if (!string.IsNullOrWhiteSpace(sn))
                query = query.Where(x => x.SN.Contains(sn));

            var dbList = query.OrderBy(x => x.Id, OrderByType.Desc).ToList();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _db?.Dispose();
        }
    }
}