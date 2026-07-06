#pragma warning disable CA1822,CS8625
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using ProjectARVRPro.Exports;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro
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

        public bool CodeUseSN { get => _CodeUseSN; set { _CodeUseSN = value; OnPropertyChanged(); } }
        private bool _CodeUseSN =true;

        public string CodeDateFormat { get => _CodeDateFormat; set { _CodeDateFormat = value; OnPropertyChanged(); } }
        private string _CodeDateFormat = "yyyyMMdd'T'HHmmss.fffffff";

        [DisplayName("按日期保存")]
        public bool SaveByDate { get => _SaveByDate; set { _SaveByDate = value; OnPropertyChanged(); } }
        private bool _SaveByDate;

        public bool IsSaveCsv { get => _IsSaveCsv; set { _IsSaveCsv = value; OnPropertyChanged(); } }
        private bool _IsSaveCsv = true;

        public bool IsSaveLink { get => _IsSaveLink; set { _IsSaveLink = value; OnPropertyChanged(); } }
        private bool _IsSaveLink = true;

        public bool IsSaveImageReuslt { get => _IsSaveImageReuslt; set { _IsSaveImageReuslt = value; OnPropertyChanged(); } }
        private bool _IsSaveImageReuslt;

        public int SaveImageReusltDelay { get => _SaveImageReusltDelay; set {  if (value>=0) _SaveImageReusltDelay = value; OnPropertyChanged(); } }
        private int _SaveImageReusltDelay = 1000;

        [DisplayName("Csv保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("ARVR")]
        public string CsvSavePath { get => _CsvSavePath; set { _CsvSavePath = value; OnPropertyChanged(); } }
        private string _CsvSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ARVR");

        [DisplayName("Text保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("ARVR")]
        public string TextSavePath { get => _TextSavePath; set { _TextSavePath = value; OnPropertyChanged(); } }
        private string _TextSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ARVR");

        [DisplayName("输出旧版ARVR格式"), Category("ARVR")]
        [Description("启用后，CSV和Socket输出将使用旧版ProjectARVR扁平格式，保持对方系统兼容")]
        public bool UseLegacyARVROutput { get => _UseLegacyARVROutput; set { _UseLegacyARVROutput = value; OnPropertyChanged(); } }
        private bool _UseLegacyARVROutput;

        [DisplayName("保存客制化XLSX"), Category("客制化输出")]
        [Description("启用后，测试完成时会在标准CSV之外追加输出指定客户格式的XLSX")]
        public bool IsSaveCustomXlsx { get => _IsSaveCustomXlsx; set { _IsSaveCustomXlsx = value; OnPropertyChanged(); } }
        private bool _IsSaveCustomXlsx;

        [DisplayName("客制化输出类型"), Category("客制化输出")]
        [Description("选择需要追加输出的客户表格格式")]
        public CustomTestResultOutputProfile CustomOutputProfile { get => _CustomOutputProfile; set { _CustomOutputProfile = value; OnPropertyChanged(); } }
        private CustomTestResultOutputProfile _CustomOutputProfile = CustomTestResultOutputProfile.金星1_0光机抽检规格_视彩成像色度计;

        [DisplayName("客制化项目名称"), Category("客制化输出")]
        [Description("用于生成每天汇总XLSX文件名，例如 2026-5-21TestResults+ProjectARVRPro.xlsx")]
        public string CustomXlsxProjectName { get => _CustomXlsxProjectName; set { _CustomXlsxProjectName = value; OnPropertyChanged(); } }
        private string _CustomXlsxProjectName = "ProjectARVRPro";

        [DisplayName("客制化XLSX保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("客制化输出")]
        [Description("客制化XLSX的输出文件夹。留空时默认使用CSV保存路径")]
        public string CustomXlsxSavePath { get => _CustomXlsxSavePath; set { _CustomXlsxSavePath = value; OnPropertyChanged(); } }
        private string _CustomXlsxSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ARVR");

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public string CustomXlsxTemplateDirectory
        {
            get => CustomXlsxSavePath;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    CustomXlsxSavePath = value;
            }
        }

        public bool ShouldSerializeCustomXlsxTemplateDirectory() => false;

    }

    public class ViewResultManager : ViewModelBase,IDisposable
    {
        private static ViewResultManager _instance;
        private static readonly object _locker = new();
        public static ViewResultManager GetInstance() { lock (_locker) { _instance ??= new ViewResultManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string SqliteDbPath { get; set; } = DirectoryPath + "ProjectARVRPro.db";

        public ViewResultManagerConfig Config { get; set; }

        public ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = new ObservableCollection<ProjectARVRReuslt>();

        public int ViewReslutsSelectedIndex { get => _ViewReslutsSelectedIndex; set { _ViewReslutsSelectedIndex = value; OnPropertyChanged(); } }
        private int _ViewReslutsSelectedIndex = -1;
        public ListView? ListView { get; set; }

        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand ViewReslutsClearCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand SlectSqlLiteDbCommand { get; set; }

        public RelayCommand SaveCommand { get; set; }

        private readonly SqlSugarClient _db;

        public ViewResultManager()
        {
            Config = ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>();
            EditConfigCommand = new RelayCommand(a => EditConfig());
            ViewReslutsClearCommand = new RelayCommand(a => ViewReslutsClear());
            QueryCommand = new RelayCommand(a => Query());
            GenericQueryCommand = new RelayCommand(a => GenericQuery());
            SlectSqlLiteDbCommand = new RelayCommand(a => SlectSqlLiteDb());



            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            // 确保表存在
            _db.CodeFirst.InitTables<ProjectARVRReuslt, ObjectiveTestResultRecord>();
            LoadAll(Config.Count);
            DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
    "sqlite.projectarvr",
    "ARVR 结果",
    () => SqliteDbPath,
    dbPath => new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = $"Data Source={dbPath}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    })));
        }
        public void SlectSqlLiteDb()
        {
            PlatformHelper.OpenFolderAndSelectFile(SqliteDbPath);
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


        /// <summary>
        /// 初始化，从数据库读取数据到ViewResluts，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            ViewResluts.Clear();
            var query = _db.Queryable<ProjectARVRReuslt>().OrderBy(x => x.Id, Config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public void Save(ProjectARVRReuslt item)
        {
            if (item == null) return;

            if (item.Id > 0)
            {
                _db.Updateable(item).ExecuteCommand();
                if (!ViewResluts.Any(x => ReferenceEquals(x, item) || x.Id == item.Id))
                    AddViewResult(item);
                return;
            }

            int id = _db.Insertable(item).ExecuteReturnIdentity();
            item.Id = id; // 更新ID
            AddViewResult(item);
        }

        private void AddViewResult(ProjectARVRReuslt item)
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
                    ListView?.ScrollIntoView(item);
                }
            }
        }

        public int SaveObjectiveTestResult(int currentRecordId, ProjectARVRReuslt result, ObjectiveTestResult objectiveTestResult)
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
            GenericQuery<ProjectARVRReuslt> genericQuery = new GenericQuery<ProjectARVRReuslt>(_db,ViewResluts);
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }

        /// <summary>
        /// 根据条件查询，举例：根据SN或Model等
        /// </summary>
        public void Query(string model = null, string sn = null, int count = -1)
        {
            ViewResluts.Clear();

            var query = _db.Queryable<ProjectARVRReuslt>();
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
