#pragma warning disable CS8625
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ProjectLUX
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

        [DisplayName("按日期保存")]
        public bool SaveByDate { get => _SaveByDate; set { _SaveByDate = value; OnPropertyChanged(); } }
        private bool _SaveByDate;

        public bool IsSaveImageReuslt { get => _IsSaveImageReuslt; set { _IsSaveImageReuslt = value; OnPropertyChanged(); } }
        private bool _IsSaveImageReuslt;

        public int SaveImageReusltDelay { get => _SaveImageReusltDelay; set { if (value >= 0) _SaveImageReusltDelay = value; OnPropertyChanged(); } }
        private int _SaveImageReusltDelay = 1000;

        [DisplayName("Csv保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("ARVR")]
        public string CsvSavePath { get => _CsvSavePath; set { _CsvSavePath = value; OnPropertyChanged(); } }
        private string _CsvSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ARVR");
    }

    public class ViewResultManager : ViewModelBase, IConfigReloadParticipant, IDisposable
    {
        private static ViewResultManager _instance;
        private static readonly object _locker = new();
        public static ViewResultManager GetInstance() { lock (_locker) { _instance ??= new ViewResultManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string SqliteDbPath { get; set; } = DirectoryPath + "ProjectLUX.db";

        private readonly RuntimeConfigOwner<ViewResultManagerConfig> configOwner;
        public ViewResultManagerConfig Config => configOwner.Current;

        public ObservableCollection<ProjectLUXReuslt> ViewResluts { get; set; } = new ObservableCollection<ProjectLUXReuslt>();

        public int ViewReslutsSelectedIndex { get => _ViewReslutsSelectedIndex; set { if (_ViewReslutsSelectedIndex == value) return; _ViewReslutsSelectedIndex = value; OnPropertyChanged(); } }
        private int _ViewReslutsSelectedIndex = -1;

        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }

        public RelayCommand SaveCommand { get; set; }

        private readonly SqlSugarClient _db;

        public ViewResultManager()
        {
            configOwner = new RuntimeConfigOwner<ViewResultManagerConfig>(
                () => ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>());
            configOwner.ConfigurationChanged += ConfigOwner_ConfigurationChanged;
            EditConfigCommand = new RelayCommand(a => EditConfig());
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
            _db.CodeFirst.InitTables<ProjectLUXReuslt, ObjectiveTestResultRecord>();
            LoadAll(Config.Count);
                DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
                    "sqlite.projectlux",
                    "LUX 结果",
                    () => SqliteDbPath,
                    dbPath => new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={dbPath}",
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute
                    })));

            try
            {
                ViewResultManagerConfig config = CaptureConfig();
                if (!Directory.Exists(config.CsvSavePath))
                    Directory.CreateDirectory(config.CsvSavePath);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }

        public ViewResultManagerConfig CaptureConfig() => configOwner.Capture();

        public string ConfigReloadName => "ProjectLUX.ViewResultManager";

        public int ConfigReloadOrder => 300;

        public void BindCurrentConfig(IConfigService currentConfig) => configOwner.BindCurrentConfig(currentConfig);

        private void ConfigOwner_ConfigurationChanged(object? sender, RuntimeConfigChangedEventArgs<ViewResultManagerConfig> e)
        {
            if (e.Generation != configOwner.Generation)
                return;
            OnPropertyChanged(nameof(Config));
        }


        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }
        public void Query()
        {
            ViewResultManagerConfig config = CaptureConfig();
            Query(null,null,config.Count);
        }

        public void Delete(int index)
        {
            ViewResluts.RemoveAt(index);
        }

        public void Save()
        {
            if (ViewResluts.Count >0 &&  ViewReslutsSelectedIndex > -1)
            {
                //if (ViewResluts[ViewReslutsSelectedIndex] is ProjectLUXReuslt kbItemMaster)
                //{
                //    string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                //    string regexPattern = $"[{Regex.Escape(invalidChars)}]";
                //    string csvpath = Config.SavePathCsv + $"\\{Regex.Replace(kbItemMaster.Model, regexPattern, "")}_{kbItemMaster.CreateTime:yyyyMMdd}.csv";
                    
                //    using var dialog = new System.Windows.Forms.SaveFileDialog();
                //    dialog.Filter = "CSV files (*.csv) | *.csv";
                //    dialog.FileName = csvpath;
                //    dialog.RestoreDirectory = true;
                //    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                //    kbItemMaster.SaveCsv(dialog.FileName);
                //}
            }

        }

        /// <summary>
        /// 初始化，从数据库读取数据到ViewResluts，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            ViewResultManagerConfig config = CaptureConfig();
            ViewResluts.Clear();
            var query = _db.Queryable<ProjectLUXReuslt>().OrderBy(x => x.Id, config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public void Save(ProjectLUXReuslt item, ViewResultManagerConfig? configSnapshot = null)
        {
            if (item == null) return;
            ViewResultManagerConfig config = configSnapshot ?? CaptureConfig();
            int id = _db.Insertable(item).ExecuteReturnIdentity();
            item.Id = id; // 更新ID

            if (config.OrderByType == OrderByType.Desc)
            {
                ViewResluts.Insert(0, item); //倒序插入
                if (config.AutoRefresh)
                {
                    ViewReslutsSelectedIndex = 0;
                }
            }
            else
            {
                ViewResluts.Add(item);
                if (config.AutoRefresh)
                {
                    ViewReslutsSelectedIndex = ViewResluts.Count - 1;
                }
            }

        }

        public int SaveObjectiveTestResult(int currentRecordId, ProjectLUXReuslt result, ObjectiveTestResult objectiveTestResult)
        {
            if (result == null || objectiveTestResult == null) return currentRecordId;

            var record = ObjectiveTestResultRecord.Create(result, objectiveTestResult);
            if (currentRecordId > 0)
            {
                var oldRecord = _db.Queryable<ObjectiveTestResultRecord>().Where(x => x.Id == currentRecordId).First();
                if (oldRecord != null)
                {
                    record.Id = currentRecordId;
                    record.CreateTime = oldRecord.CreateTime;
                    _db.Updateable(record).Where(x => x.Id == record.Id).ExecuteCommand();
                    return record.Id;
                }
            }

            record.Id = _db.Insertable(record).ExecuteReturnIdentity();
            return record.Id;
        }

        public List<ObjectiveTestResultRecord> QueryObjectiveTestResultRecords(string sn = null, int count = 100)
        {
            var query = _db.Queryable<ObjectiveTestResultRecord>();
            if (!string.IsNullOrWhiteSpace(sn))
            {
                query = query.Where(x => x.SN.Contains(sn));
            }

            query = query.OrderBy(x => x.Id, OrderByType.Desc);
            return count > 0 ? query.Take(count).ToList() : query.ToList();
        }

        public void GenericQuery()
        {
            GenericQuery<ProjectLUXReuslt> genericQuery = new GenericQuery<ProjectLUXReuslt>(_db,ViewResluts);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }

        /// <summary>
        /// 根据条件查询，举例：根据SN或Model等
        /// </summary>
        public void Query(string model = null, string sn = null, int count = -1)
        {
            ViewResultManagerConfig config = CaptureConfig();
            ViewResluts.Clear();

            var query = _db.Queryable<ProjectLUXReuslt>();
            query = query.OrderBy(x => x.Id, config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();

            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public void Dispose()
        {
            _db?.Dispose();
            configOwner.ConfigurationChanged -= ConfigOwner_ConfigurationChanged;
            configOwner.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
