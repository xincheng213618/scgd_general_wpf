#pragma warning disable CS8625
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI;
using ProjectKB.Auth;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace ProjectKB
{
    public class ViewResultManagerConfig : ViewModelBase, IConfig
    {
        [DisplayName("查询数量"), Category("View")]
        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count = 50;

        [DisplayName("按类型排序"), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; OnPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;

        [DisplayName("自动刷新"), Category("View")]
        public bool AutoRefresh { get => _AutoRefresh; set { _AutoRefresh = value; OnPropertyChanged(); } }
        private bool _AutoRefresh = true;

        [DisplayName("视图高度"), Category("View")]
        public double Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private double _Height = 300;


        [DisplayName("LV CSV保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("KB")]
        public string CsvSavePath { get => _CsvSavePath; set { _CsvSavePath = value; OnPropertyChanged(); } }
        private string _CsvSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KB");

        [DisplayName("自动导出LV CSV"), Category("KB")]
        public bool AutoSaveLvCsv { get => _AutoSaveLvCsv; set { _AutoSaveLvCsv = value; OnPropertyChanged(); } }
        private bool _AutoSaveLvCsv = true;

        [DisplayName("LC CSV保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("KB")]
        public string LcCsvSavePath { get => _LcCsvSavePath; set { _LcCsvSavePath = value; OnPropertyChanged(); } }
        private string _LcCsvSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KB");

        [DisplayName("自动导出LC CSV"), Category("KB")]
        public bool AutoSaveLcCsv { get => _AutoSaveLcCsv; set { _AutoSaveLcCsv = value; OnPropertyChanged(); } }
        private bool _AutoSaveLcCsv = true;

        [DisplayName("Text保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("KB")]
        public string TextSavePath { get => _TextSavePath; set { _TextSavePath = value; OnPropertyChanged(); } }
        private string _TextSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KB");

        [DisplayName("保存Text"), Category("KB")]
        public bool SaveText { get => _SaveText; set { _SaveText = value; OnPropertyChanged(); } }
        private bool _SaveText = true;

        [DisplayName("Summary保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("KB")]
        public string SummarySavePath { get => _SummarySavePath; set { _SummarySavePath = value; OnPropertyChanged(); } }
        private string _SummarySavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KB", "Summary");

        [DisplayName("保存Summary"), Category("KB")]
        public bool SaveSummary { get => _SaveSummary; set { _SaveSummary = value; OnPropertyChanged(); } }
        private bool _SaveSummary = true;

        [DisplayName("CSV追加Fallout统计"), Category("KB")]
        [Description("启用后会在 CSV 末尾追加 Fallout= 与通过率统计，并在后续追加结果时自动重算该行")]
        public bool AppendFalloutSummary { get => _AppendFalloutSummary; set { _AppendFalloutSummary = value; OnPropertyChanged(); } }
        private bool _AppendFalloutSummary = true;
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

        public int ViewReslutsSelectedIndex { get => _ViewReslutsSelectedIndex; set { if (_ViewReslutsSelectedIndex == value) return; _ViewReslutsSelectedIndex = value; OnPropertyChanged(); } }
        private int _ViewReslutsSelectedIndex = -1;

        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }

        public RelayCommand SaveCommand { get; set; }
        public RelayCommand SaveLcCommand { get; set; }

        private readonly SqlSugarClient _db;

        public ViewResultManager()
        {
            Config = ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>();
            EditConfigCommand = new RelayCommand(a => EditConfig());
            QueryCommand = new RelayCommand(a => Query());
            GenericQueryCommand = new RelayCommand(a => GenericQuery());
            SaveCommand = new RelayCommand(a => Save(KBCsvDataType.Lv));
            SaveLcCommand = new RelayCommand(a => Save(KBCsvDataType.Lc));
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath};Default Timeout=5",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            _db.Ado.ExecuteCommand("PRAGMA busy_timeout = 5000;");
            _db.Ado.ExecuteCommand("PRAGMA journal_mode = WAL;");
            // 确保表存在
            _db.CodeFirst.InitTables<KBItemMaster, KBProductionSession>();
            KBResultPayloadStorage.EnsureSchema(_db);
            LoadAll(Config.Count);
        }


        public void EditConfig()
        {
            if (!RequireAdmin()) return;

            new PropertyEditorWindow(Config) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }

        public void Query()
        {
            if (!RequireAdmin()) return;

            QueryCore(null,null,Config.Count);
        }

        public void Delete(int index)
        {
            if (!RequireAdmin()) return;
            if (index < 0 || index >= ViewResluts.Count) return;

            ViewResluts.RemoveAt(index);
        }

        public void Save(KBCsvDataType dataType)
        {
            if (!RequireAdmin()) return;

            if (ViewResluts.Count >0 &&  ViewReslutsSelectedIndex > -1)
            {
                if (ViewResluts[ViewReslutsSelectedIndex] is KBItemMaster kbItemMaster)
                {
                    try
                    {
                        LoadResultPayload(kbItemMaster);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            Application.Current.GetActiveWindow(),
                            $"结果明细读取失败，无法重新导出：{ex.Message}",
                            "ProjectKB");
                        return;
                    }
                    string savePath = dataType == KBCsvDataType.Lv ? Config.CsvSavePath : Config.LcCsvSavePath;
                    string csvpath = BuildCsvPath(kbItemMaster, savePath, dataType);
                    
                    using var dialog = new System.Windows.Forms.SaveFileDialog();
                    dialog.Filter = "CSV files (*.csv) | *.csv";
                    dialog.FileName = csvpath;
                    dialog.RestoreDirectory = true;
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    kbItemMaster.SaveCsv(dialog.FileName, dataType, Config.AppendFalloutSummary);
                }
            }

        }

        internal static string BuildCsvPath(KBItemMaster item, string savePath, KBCsvDataType dataType)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            string regexPattern = $"[{Regex.Escape(invalidChars)}]";
            string safeModel = Regex.Replace(item.Model ?? string.Empty, regexPattern, "");
            string suffix = dataType == KBCsvDataType.Lv ? "LV" : "LC";
            return Path.Combine(savePath, $"{safeModel}_{item.CreateTime:yyyyMMdd}-{suffix}.csv");
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

            KBResultImageDimensions.TryPopulate(item);
            bool isNew = item.Id <= 0;
            bool savePayload = isNew || item.IsResultPayloadLoaded;
            KBResultPayloadStorage.RunDatabaseMaintenance(() =>
            {
                _db.Ado.BeginTran();
                try
                {
                    if (isNew)
                    {
                        item.Id = _db.Insertable(item).ExecuteReturnIdentity();
                    }
                    else
                    {
                        _db.Updateable(item).ExecuteCommand();
                    }

                    if (savePayload)
                        KBResultPayloadStorage.SaveResult(_db, item);
                    _db.Ado.CommitTran();
                }
                catch
                {
                    _db.Ado.RollbackTran();
                    if (isNew)
                        item.Id = 0;
                    throw;
                }
            });

            if (isNew || !ViewResluts.Any(x => ReferenceEquals(x, item) || x.Id == item.Id))
                AddViewResult(item);
        }

        public bool UpdateImageDimensions(KBItemMaster item, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (width <= 0 || height <= 0)
                return false;
            if (item.ImageWidth == width && item.ImageHeight == height)
                return false;

            if (item.Id > 0)
            {
                KBResultPayloadStorage.RunDatabaseMaintenance(() =>
                    _db.Updateable<KBItemMaster>()
                        .SetColumns(result => new KBItemMaster
                        {
                            ImageWidth = width,
                            ImageHeight = height,
                        })
                        .Where(result => result.Id == item.Id)
                        .ExecuteCommand());
            }

            item.ImageWidth = width;
            item.ImageHeight = height;

            return true;
        }

        public void LoadResultPayload(KBItemMaster item)
        {
            ArgumentNullException.ThrowIfNull(item);
            KBResultPayloadStorage.LoadResult(_db, item);
        }

        private void AddViewResult(KBItemMaster item)
        {
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
                }
            }
        }

        public void GenericQuery()
        {
            if (!RequireAdmin()) return;

            GenericQuery<KBItemMaster> genericQuery = new GenericQuery<KBItemMaster>(_db,ViewResluts);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }

        /// <summary>
        /// 根据条件查询，举例：根据SN或Model等
        /// </summary>
        public void Query(string model = null, string sn = null, int count = -1)
        {
            if (!RequireAdmin()) return;

            QueryCore(model, sn, count);
        }

        private void QueryCore(string model = null, string sn = null, int count = -1)
        {
            ViewResluts.Clear();

            var query = _db.Queryable<KBItemMaster>();
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

        private static bool RequireAdmin()
        {
            return KBAuthManager.GetInstance().RequireAdmin(Application.Current.GetActiveWindow());
        }
    }
}
