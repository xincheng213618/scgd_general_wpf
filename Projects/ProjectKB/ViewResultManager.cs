using ColorVision.Common.MVVM;
using ColorVision.Engine;
using ColorVision.UI;
using NPOI.SS.Formula.Functions;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ProjectKB
{
    public class ViewResultManagerConfig : ViewModelBase, IConfig
    {
        [DisplayName("查询数量"), Category("View")]
        public int Count { get => _Count; set { _Count = value; NotifyPropertyChanged(); } }
        private int _Count = -1;

        [DisplayName("按类型排序"), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; NotifyPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;

        [DisplayName("自动刷新"), Category("View")]
        public bool AutoRefresh { get => _AutoRefresh; set { _AutoRefresh = value; NotifyPropertyChanged(); } }
        private bool _AutoRefresh = true;

        [DisplayName("视图高度"), Category("View")]
        public double Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private double _Height = 300;

        [DisplayName("打开图像延迟"), Category("View")]
        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; NotifyPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

        [DisplayName("Csv保存路径"), PropertyEditorType(PropertyEditorType.TextSelectFolder), Category("KB")]
        public string SavePathCsv { get => _SavePathCsv; set { _SavePathCsv = value; NotifyPropertyChanged(); } }
        private string _SavePathCsv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KB");

        [DisplayName("Text保存路径"), PropertyEditorType(PropertyEditorType.TextSelectFolder), Category("KB")]
        public string SavePathText { get => _SavePathText; set { _SavePathText = value; NotifyPropertyChanged(); } }
        private string _SavePathText = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KB");
    }

    public class ViewResultManager : ViewModelBase,IDisposable
    {
        private static ViewResultManager _instance;
        private static readonly object _locker = new();
        public static ViewResultManager GetInstance() { lock (_locker) { _instance ??= new ViewResultManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string SqliteDbPath { get; set; } = DirectoryPath + "ProjectKB.db";

        public ViewResultManagerConfig Config { get; set; }

        public ObservableCollection<KBItemMaster> ViewResluts { get; set; } = new ObservableCollection<KBItemMaster>();

        public int ViewReslutsSelectedIndex { get => _ViewReslutsSelectedIndex; set { _ViewReslutsSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _ViewReslutsSelectedIndex = -1;
        public ListView ListView { get; set; }

        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand ViewReslutsClearCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }

        public RelayCommand SaveCommand { get; set; }

        private readonly SqlSugarClient _db;

        public ViewResultManager()
        {
            Config = ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>();
            EditConfigCommand = new RelayCommand(a => EditConfig());
            ViewReslutsClearCommand = new RelayCommand(a => ViewReslutsClear());
            QueryCommand = new RelayCommand(a => Query());
            GenericQueryCommand = new RelayCommand(a => GenericQuery());
            SaveCommand = new RelayCommand(a => Save());
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


        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }
        public void ViewReslutsClear()
        {
            ViewReslutsSelectedIndex = -1;
            ViewResluts.Clear();
        }
        public void Query()
        {
            Query(null,null,Config.Count);
        }

        public void Delete(int index)
        {
            ViewResluts.RemoveAt(index);
        }

        public void Save()
        {
            if (ViewResluts.Count >0 &&  ViewReslutsSelectedIndex > -1)
            {
                if (ViewResluts[ViewReslutsSelectedIndex] is KBItemMaster kbItemMaster)
                {
                    string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                    string regexPattern = $"[{Regex.Escape(invalidChars)}]";
                    string csvpath = Config.SavePathCsv + $"\\{Regex.Replace(kbItemMaster.Model, regexPattern, "")}_{kbItemMaster.CreateTime:yyyyMMdd}.csv";
                    
                    using var dialog = new System.Windows.Forms.SaveFileDialog();
                    dialog.Filter = "CSV files (*.csv) | *.csv";
                    dialog.FileName = csvpath;
                    dialog.RestoreDirectory = true;
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    kbItemMaster.SaveCsv(dialog.FileName);
                }
            }

        }

        /// <summary>
        /// 初始化，从数据库读取数据到ViewResluts，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            ViewResluts.Clear();
            var query = _db.Queryable<KBItemMaster>().OrderBy(x => x.Id, Config.OrderByType);
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

            if (Config.OrderByType == OrderByType.Desc)
            {
                ViewResluts.Insert(0, item); //倒序插入
                if (Config.AutoRefresh)
                {
                    ViewReslutsSelectedIndex = 0;
                }
            }
            else
            {
                ViewResluts.Add(item);
                if (Config.AutoRefresh)
                {
                    ViewReslutsSelectedIndex = ViewResluts.Count - 1;
                    ListView?.ScrollIntoView(item);
                }
            }

        }

        public void GenericQuery()
        {
            GenericQuery<KBItemMaster> genericQuery = new GenericQuery<KBItemMaster>(_db,ViewResluts);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }

        /// <summary>
        /// 根据条件查询，举例：根据SN或Model等
        /// </summary>
        public void Query(string model = null, string sn = null, int count = -1)
        {
            ViewResluts.Clear();
            var query = _db.Queryable<KBItemMaster>();

            if (!string.IsNullOrWhiteSpace(model))
                query = query.Where(x => x.Model.Contains(model));
            if (!string.IsNullOrWhiteSpace(sn))
                query = query.Where(x => x.SN.Contains(sn));

             query = query.OrderBy(x => x.Id, Config.OrderByType);
            
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();

            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public void Dispose()
        {
            _db?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}