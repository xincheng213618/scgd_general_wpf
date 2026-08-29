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
    public enum ResultImageFormat
    {
        PNG = 0,
        JPEG = 1,
    }

    public enum SourceImageFormat
    {
        TIFF = 0,
        PNG = 1,
        BMP = 2,
    }

    public enum SourceImageHighBitFormat
    {
        TIFF = 0,
        PNG = 1,
    }

    public enum SourceTiffCompression
    {
        LZW = 5,
        ZIP = 8,
    }

    public enum ImageExportSize
    {
        完整尺寸 = 0,
        二分之一尺寸 = 2,
        四分之一尺寸 = 4,
    }

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

        [DisplayName("保存标记图（8位）"), Category("图像导出")]
        [Description("保存8位结果图；可选择是否把点位、文字等标记混合到图中")]
        public bool IsSaveImageReuslt
        {
            get => _IsSaveImageReuslt;
            set { _IsSaveImageReuslt = value; OnPropertyChanged(); }
        }
        private bool _IsSaveImageReuslt;

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [Newtonsoft.Json.JsonIgnore]
        [Obsolete("结果快照已改为立即异步保存，不再使用保存延时。")]
        public int SaveImageReusltDelay
        {
            get => 0;
            set { }
        }

        [DisplayName("标记图格式"), Category("图像导出")]
        [Description("PNG无损；JPEG固定质量100、编码更快但属于有损格式")]
        [PropertyVisibility(nameof(IsSaveImageReuslt))]
        public ResultImageFormat ResultSnapshotFormat
        {
            get => _ResultSnapshotFormat;
            set
            {
                _ResultSnapshotFormat = value is ResultImageFormat.PNG or ResultImageFormat.JPEG
                    ? value
                    : ResultImageFormat.PNG;
                OnPropertyChanged();
            }
        }
        private ResultImageFormat _ResultSnapshotFormat = ResultImageFormat.PNG;

        [DisplayName("标记图尺寸"), Category("图像导出")]
        [Description("完整、1/2或1/4宽高；缩小只影响导出耗时和文件大小，不影响测量结果")]
        [PropertyVisibility(nameof(IsSaveImageReuslt))]
        public ImageExportSize ResultSnapshotSize
        {
            get => _ResultSnapshotSize;
            set
            {
                _ResultSnapshotSize = value switch
                {
                    ImageExportSize.完整尺寸 or ImageExportSize.二分之一尺寸 or ImageExportSize.四分之一尺寸 => value,
                    _ when (int)value == 4096 => ImageExportSize.二分之一尺寸,
                    _ => ImageExportSize.完整尺寸,
                };
                OnPropertyChanged();
            }
        }
        private ImageExportSize _ResultSnapshotSize = ImageExportSize.完整尺寸;

        [DisplayName("混合保存标记"), Category("图像导出")]
        [Description("开启时将点位、文字等标记混合到结果图；关闭时只保存底图")]
        [PropertyVisibility(nameof(IsSaveImageReuslt))]
        public bool ResultSnapshotIncludeOverlays { get => _ResultSnapshotIncludeOverlays; set { _ResultSnapshotIncludeOverlays = value; OnPropertyChanged(); } }
        private bool _ResultSnapshotIncludeOverlays = true;

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [Newtonsoft.Json.JsonIgnore]
        [Obsolete("JPEG质量固定为100，不再向用户开放压缩参数。")]
        public int ResultSnapshotJpegQuality
        {
            get => 100;
            set { }
        }

        [DisplayName("保存原图（保留位深）"), Category("图像导出")]
        [Description("直接保存ImageEditor当前已加载的原始像素，不混合标记、不改变尺寸；可与8位标记图同时保存")]
        public bool IsSaveSourceImage
        {
            get => _IsSaveSourceImage;
            set
            {
                _IsSaveSourceImage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSourceFormatWithBmp));
                OnPropertyChanged(nameof(ShowSourceFormatWithoutBmp));
                OnPropertyChanged(nameof(ShowSourceTiffCompression));
            }
        }
        private bool _IsSaveSourceImage;

        [Browsable(false)]
        public SourceImageFormat SourceExportFormat
        {
            get => _SourceImageFormat;
            set
            {
                _SourceImageFormat = value is SourceImageFormat.TIFF or SourceImageFormat.PNG or SourceImageFormat.BMP
                    ? value
                    : SourceImageFormat.TIFF;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceExportFormatWithBmp));
                OnPropertyChanged(nameof(SourceExportFormatWithoutBmp));
                OnPropertyChanged(nameof(ShowSourceTiffCompression));
            }
        }
        private SourceImageFormat _SourceImageFormat = SourceImageFormat.TIFF;

        [DisplayName("原图格式"), Category("图像导出")]
        [Description("TIFF和PNG保留源图位深；BMP仅在当前ImageEditor源图可无损表示为8位格式时提供")]
        [PropertyVisibility(nameof(ShowSourceFormatWithBmp))]
        [Newtonsoft.Json.JsonIgnore]
        public SourceImageFormat SourceExportFormatWithBmp
        {
            get => SourceExportFormat;
            set => SourceExportFormat = value;
        }

        [DisplayName("原图格式"), Category("图像导出")]
        [Description("当前ImageEditor源图为高位深格式；PNG和TIFF可保留源图位深，BMP不提供")]
        [PropertyVisibility(nameof(ShowSourceFormatWithoutBmp))]
        [Newtonsoft.Json.JsonIgnore]
        public SourceImageHighBitFormat SourceExportFormatWithoutBmp
        {
            get => SourceExportFormat == SourceImageFormat.PNG
                ? SourceImageHighBitFormat.PNG
                : SourceImageHighBitFormat.TIFF;
            set => SourceExportFormat = value == SourceImageHighBitFormat.PNG
                ? SourceImageFormat.PNG
                : SourceImageFormat.TIFF;
        }

        [Browsable(false), Newtonsoft.Json.JsonIgnore]
        public bool SourceImageSupportsBmp
        {
            get => _SourceImageSupportsBmp;
            set
            {
                if (_SourceImageSupportsBmp == value)
                    return;

                _SourceImageSupportsBmp = value;
                if (!value && SourceExportFormat == SourceImageFormat.BMP)
                    SourceExportFormat = SourceImageFormat.TIFF;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowSourceFormatWithBmp));
                OnPropertyChanged(nameof(ShowSourceFormatWithoutBmp));
            }
        }
        private bool _SourceImageSupportsBmp;

        [Browsable(false), Newtonsoft.Json.JsonIgnore]
        public bool ShowSourceFormatWithBmp => IsSaveSourceImage && SourceImageSupportsBmp;

        [Browsable(false), Newtonsoft.Json.JsonIgnore]
        public bool ShowSourceFormatWithoutBmp => IsSaveSourceImage && !SourceImageSupportsBmp;

        [DisplayName("TIFF压缩"), Category("图像导出")]
        [Description("LZW为推荐默认；ZIP文件略小但可能慢很多。两者均为无损压缩并保留源图位深")]
        [PropertyVisibility(nameof(ShowSourceTiffCompression))]
        public SourceTiffCompression SourceTiffCompressionMode
        {
            get => _SourceTiffCompression;
            set
            {
                _SourceTiffCompression = value is SourceTiffCompression.LZW or SourceTiffCompression.ZIP
                    ? value
                    : SourceTiffCompression.LZW;
                OnPropertyChanged();
            }
        }
        private SourceTiffCompression _SourceTiffCompression = SourceTiffCompression.LZW;

        [Browsable(false), Newtonsoft.Json.JsonIgnore]
        public bool ShowSourceTiffCompression => IsSaveSourceImage && SourceExportFormat == SourceImageFormat.TIFF;

        [DisplayName("Csv保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("ARVR")]
        public string CsvSavePath { get => _CsvSavePath; set { _CsvSavePath = value; OnPropertyChanged(); } }
        private string _CsvSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ARVR");
    }

    public class ViewResultManager : ViewModelBase,IDisposable
    {
        private static ViewResultManager _instance;
        private static readonly object _locker = new();
        public static ViewResultManager GetInstance() { lock (_locker) { _instance ??= new ViewResultManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string SqliteDbPath { get; set; } = DirectoryPath + "ProjectLUX.db";

        public ViewResultManagerConfig Config { get; set; }

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
            Config = ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>();
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
                if (!Directory.Exists(Config.CsvSavePath))
                    Directory.CreateDirectory(Config.CsvSavePath);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }


        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
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
            ViewResluts.Clear();
            var query = _db.Queryable<ProjectLUXReuslt>().OrderBy(x => x.Id, Config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public void Save(ProjectLUXReuslt item)
        {
            if (item == null) return;
            if (item.Id <= 0
                && ResultImageDimensions.TryReadFromMeasureResults(item.BatchId, item.FileName, out int width, out int height))
            {
                item.ImageWidth = width;
                item.ImageHeight = height;
            }

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
                }
            }

        }

        internal bool UpdateSavedImagePaths(
            ProjectLUXReuslt item,
            ResultImageExportPathUpdate update)
        {
            ArgumentNullException.ThrowIfNull(item);
            return ApplySavedImagePathUpdate(item, update, (savedResultImageFileName, savedSourceImageFileName) =>
            {
                int updatedRows = _db.Updateable<ProjectLUXReuslt>()
                    .SetColumns(result => new ProjectLUXReuslt
                    {
                        SavedResultImageFileName = savedResultImageFileName,
                        SavedSourceImageFileName = savedSourceImageFileName,
                    })
                    .Where(result => result.Id == item.Id)
                    .ExecuteCommand();
                if (updatedRows != 1)
                    throw new InvalidOperationException($"未能更新结果图像路径：resultId={item.Id}, affectedRows={updatedRows}");
            });
        }

        internal static bool ApplySavedImagePathUpdate(
            ProjectLUXReuslt item,
            ResultImageExportPathUpdate update,
            Action<string?, string?> persist)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(persist);

            string? savedResultImageFileName = update.UpdateSavedResultImageFileName
                ? update.SavedResultImageFileName
                : item.SavedResultImageFileName;
            string? savedSourceImageFileName = update.UpdateSavedSourceImageFileName
                ? update.SavedSourceImageFileName
                : item.SavedSourceImageFileName;
            bool resultPathChanged = update.UpdateSavedResultImageFileName
                && !string.Equals(item.SavedResultImageFileName, savedResultImageFileName, StringComparison.OrdinalIgnoreCase);
            bool sourcePathChanged = update.UpdateSavedSourceImageFileName
                && !string.Equals(item.SavedSourceImageFileName, savedSourceImageFileName, StringComparison.OrdinalIgnoreCase);
            if (!resultPathChanged && !sourcePathChanged)
                return false;

            if (item.Id > 0)
                persist(savedResultImageFileName, savedSourceImageFileName);

            item.SavedResultImageFileName = savedResultImageFileName;
            item.SavedSourceImageFileName = savedSourceImageFileName;
            return true;
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
            ViewResluts.Clear();

            var query = _db.Queryable<ProjectLUXReuslt>();
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
