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

namespace ProjectARVRPro
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

        [DisplayName("保存标记图（8位）"), Category("图像导出")]
        [Description("保存8位结果图；可选择是否把点位、文字等标记混合到图中")]
        public bool IsSaveImageReuslt
        {
            get => _IsSaveImageReuslt;
            set
            {
                _IsSaveImageReuslt = value;
                OnPropertyChanged();
            }
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
        [Description("PNG无损并兼容原有result.png；JPEG固定质量100、编码更快但属于有损格式")]
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
        [Description("完整、1/2或1/4宽高；缩小仅用于降低导出耗时和文件大小，不影响测量数据与算法结果")]
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
        [Description("LZW为推荐默认；ZIP文件仅略小但速度可能慢很多。两者均为无损压缩并保留源图位深")]
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

        public RelayCommand QueryCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand SlectSqlLiteDbCommand { get; set; }

        private readonly SqlSugarClient _db;

        public ViewResultManager()
        {
            Config = ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>();
            QueryCommand = new RelayCommand(a => Query());
            GenericQueryCommand = new RelayCommand(a => GenericQuery());
            SlectSqlLiteDbCommand = new RelayCommand(a => SlectSqlLiteDb());



            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath};Default Timeout=5",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            _db.Ado.ExecuteCommand("PRAGMA busy_timeout = 5000;");
            _db.Ado.ExecuteCommand("PRAGMA journal_mode = WAL;");
            // 确保表存在
            _db.CodeFirst.InitTables<ProjectARVRReuslt, ObjectiveTestResultRecord>();
            ResultJsonPayloadStorage.EnsureSchema(_db);
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
            var query = _db.Queryable<ProjectARVRReuslt>().OrderBy(x => x.Id, OrderByType.Desc);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

        public ProjectARVRReuslt? FindByBatchId(int batchId)
        {
            if (batchId <= 0)
                return null;

            ProjectARVRReuslt? loadedResult = ViewResluts.FirstOrDefault(item => item.BatchId == batchId);
            return loadedResult
                ?? _db.Queryable<ProjectARVRReuslt>()
                    .Where(item => item.BatchId == batchId)
                    .OrderBy(item => item.Id, OrderByType.Desc)
                    .First();
        }

        public void Save(ProjectARVRReuslt item)
        {
            if (item == null) return;

            bool isNew = item.Id <= 0;
            if (isNew
                && ResultImageDimensions.TryReadFromMeasureResults(item.BatchId, item.FileName, out int width, out int height))
            {
                item.ImageWidth = width;
                item.ImageHeight = height;
            }

            bool savePayload = item.ViewResultJson != null;
            ResultJsonPayloadStorage.RunDatabaseMaintenance(() =>
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
                        ResultJsonPayloadStorage.SaveViewResultJson(_db, item.Id, item.ViewResultJson);
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

        internal bool UpdateSavedImagePaths(
            ProjectARVRReuslt item,
            ResultImageExportPathUpdate update)
        {
            ArgumentNullException.ThrowIfNull(item);
            return ApplySavedImagePathUpdate(item, update, (savedResultImageFileName, savedSourceImageFileName) =>
            {
                int updatedRows = ResultJsonPayloadStorage.RunDatabaseMaintenance(() =>
                    _db.Updateable<ProjectARVRReuslt>()
                        .SetColumns(result => new ProjectARVRReuslt
                        {
                            SavedResultImageFileName = savedResultImageFileName,
                            SavedSourceImageFileName = savedSourceImageFileName,
                        })
                        .Where(result => result.Id == item.Id)
                        .ExecuteCommand());
                if (updatedRows != 1)
                    throw new InvalidOperationException($"未能更新结果图像路径：resultId={item.Id}, affectedRows={updatedRows}");
            });
        }

        internal static bool ApplySavedImagePathUpdate(
            ProjectARVRReuslt item,
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

        public string? LoadViewResultJson(ProjectARVRReuslt item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.ViewResultJson == null && item.Id > 0)
                item.ViewResultJson = ResultJsonPayloadStorage.LoadViewResultJson(_db, item.Id) ?? string.Empty;
            return item.ViewResultJson;
        }

        public IReadOnlyList<ProjectARVRReuslt> GetObjectiveTestFlowResults(int objectiveRecordId)
        {
            if (objectiveRecordId <= 0)
                return Array.Empty<ProjectARVRReuslt>();

            return ResultJsonPayloadStorage.RunDatabaseMaintenance(() =>
            {
                ObjectiveTestResultRecord? record = _db.Queryable<ObjectiveTestResultRecord>()
                    .Where(item => item.Id == objectiveRecordId)
                    .First();
                if (record == null || record.ResultId <= 0 || string.IsNullOrWhiteSpace(record.SN))
                    return (IReadOnlyList<ProjectARVRReuslt>)Array.Empty<ProjectARVRReuslt>();

                int previousResultId = _db.Queryable<ObjectiveTestResultRecord>()
                    .Where(item => item.SN == record.SN && item.Id < record.Id)
                    .OrderBy(item => item.Id, OrderByType.Desc)
                    .Select(item => item.ResultId)
                    .First();
                List<ProjectARVRReuslt> results = _db.Queryable<ProjectARVRReuslt>()
                    .Where(item => item.SN == record.SN && item.Id > previousResultId && item.Id <= record.ResultId)
                    .OrderBy(item => item.Id, OrderByType.Asc)
                    .ToList();
                ResultJsonPayloadStorage.LoadViewResultJsons(_db, results);
                return results;
            });
        }

        private void AddViewResult(ProjectARVRReuslt item)
        {
            ViewResluts.Insert(0, item);
        }

        public int SaveObjectiveTestResult(int currentRecordId, ProjectARVRReuslt result, ObjectiveTestResult objectiveTestResult)
        {
            if (result == null || objectiveTestResult == null) return currentRecordId;

            var record = ObjectiveTestResultRecord.Create(result, objectiveTestResult);
            return ResultJsonPayloadStorage.RunDatabaseMaintenance(() =>
            {
                if (currentRecordId > 0)
                {
                    var oldRecord = _db.Queryable<ObjectiveTestResultRecord>()
                        .Where(x => x.Id == currentRecordId)
                        .Select(x => new ObjectiveTestResultRecord
                        {
                            Id = x.Id,
                            CreateTime = x.CreateTime,
                            IsFinalized = x.IsFinalized,
                        })
                        .First();
                    if (oldRecord != null)
                    {
                        record.Id = currentRecordId;
                        record.CreateTime = oldRecord.CreateTime;
                        record.IsFinalized = oldRecord.IsFinalized;
                    }
                }

                _db.Ado.BeginTran();
                try
                {
                    if (record.Id > 0)
                        _db.Updateable(record).Where(x => x.Id == record.Id).ExecuteCommand();
                    else
                        record.Id = _db.Insertable(record).ExecuteReturnIdentity();

                    ResultJsonPayloadStorage.SaveObjectiveTestResultJson(_db, record.Id, record.ObjectiveTestResultJson);
                    _db.Ado.CommitTran();
                    return record.Id;
                }
                catch
                {
                    _db.Ado.RollbackTran();
                    throw;
                }
            });
        }

        public int FinalizeObjectiveTestResult(int recordId, DateTime completedAt)
        {
            if (recordId <= 0)
                return 0;

            return ResultJsonPayloadStorage.RunDatabaseMaintenance(() =>
            {
                using SqlSugarClient db = CreateSqliteClient();
                return db.Updateable<ObjectiveTestResultRecord>()
                    .SetColumns(item => item.IsFinalized == true)
                    .SetColumns(item => item.UpdateTime == completedAt)
                    .Where(item => item.Id == recordId)
                    .ExecuteCommand();
            });
        }

        public IReadOnlyList<CycleTimeGroup> QueryCycleTimeGroups(
            DateTime? from = null,
            DateTime? toExclusive = null,
            string? sn = null,
            bool? result = null,
            int count = -1)
        {
            using SqlSugarClient db = CreateSqliteClient();
            ISugarQueryable<ProjectARVRReuslt> query = db.Queryable<ProjectARVRReuslt>()
                .Where(item => item.SN != string.Empty);
            if (from.HasValue)
            {
                // Include a lead-in window so an execution crossing midnight is grouped before
                // applying the completion-time filter below. This legacy table has no stable
                // execution id, so retain the prior one-day boundary rather than silently
                // truncating a long prepared/session run.
                DateTime scanFrom = from.Value.Date.AddDays(-1);
                query = query.Where(item => item.CreateTime >= scanFrom);
            }
            if (toExclusive.HasValue)
            {
                DateTime scanTo = toExclusive.Value.Date.AddDays(1);
                query = query.Where(item => item.CreateTime < scanTo);
            }
            if (!string.IsNullOrWhiteSpace(sn))
                query = query.Where(item => item.SN.Contains(sn));

            List<CycleTimeResultSample> samples = query
                .Select(item => new CycleTimeResultSample
                {
                    Id = item.Id,
                    SN = item.SN,
                    TestType = item.TestType,
                    Result = item.Result,
                    RunTime = item.RunTime,
                    CreateTime = item.CreateTime
                })
                .ToList();

            IEnumerable<CycleTimeGroup> groups = CycleTimeCalculator.Calculate(samples);
            if (from.HasValue)
                groups = groups.Where(group => group.LastTime >= from.Value);
            if (toExclusive.HasValue)
                groups = groups.Where(group => group.LastTime < toExclusive.Value);
            if (result.HasValue)
                groups = groups.Where(group => group.Result == result.Value);
            if (count > 0)
                groups = groups.Take(count);
            return groups.ToList();
        }

        public IReadOnlyList<ProjectARVRReuslt> QueryCycleTimeDetails(CycleTimeGroup group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.SN))
            {
                return [];
            }

            using SqlSugarClient db = CreateSqliteClient();
            return db.Queryable<ProjectARVRReuslt>()
                .Where(item => item.SN == group.SN && item.Id >= group.FirstId && item.Id <= group.LatestId)
                .OrderBy(item => item.Id, OrderByType.Asc)
                .ToList();
        }

        private static SqlSugarClient CreateSqliteClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath};Default Timeout=5",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
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
            if (!string.IsNullOrWhiteSpace(model))
                query = query.Where(item => item.Model.Contains(model.Trim()));
            if (!string.IsNullOrWhiteSpace(sn))
                query = query.Where(item => item.SN.Contains(sn.Trim()));
            query = query.OrderBy(x => x.Id, OrderByType.Desc);
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
