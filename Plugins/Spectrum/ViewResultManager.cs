using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI;
using cvColorVision;
using Newtonsoft.Json;
using ScottPlot.Plottables;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum
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

        [DisplayName("打开图像延迟"), Category("View")]
        public int ViewImageReadDelay { get => _ViewImageReadDelay; set { _ViewImageReadDelay = value; OnPropertyChanged(); } }
        private int _ViewImageReadDelay = 1000;

        [DisplayName("Csv保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor)), Category("Spectrum")]
        public string SavePathCsv { get => _SavePathCsv; set { _SavePathCsv = value; OnPropertyChanged(); } }
        private string _SavePathCsv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Spectrum");

    }

    [SugarTable("Sprectrum")]
    public class SprectrumModel : ViewModelBase,IEntity
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [DisplayName("创建时间")]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        [SugarColumn(IsIgnore =true)]
        public COLOR_PARA ColorParam { get; set; }

        public string ColorParamJson
        {
            get => JsonConvert.SerializeObject(ColorParam);
            set
            {
                if (!string.IsNullOrEmpty(value))
                    ColorParam = JsonConvert.DeserializeObject<COLOR_PARA>(value);
            }
        }

        // EQE / 光通量模式 persisted fields
        [SugarColumn(IsNullable = true)]
        public float? EqeVoltage { get; set; }

        [SugarColumn(IsNullable = true)]
        public float? EqeCurrentMA { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? Eqe { get; set; }

        [SugarColumn(IsNullable = true)]
        public float? LuminousFlux { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? RadiantFlux { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? LuminousEfficacy { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? ExcitationPurity { get; set; }

        /// <summary>
        /// 是否为重新计算的数据 (true = 用户手动重新计算, false/null = 原始测量数据)
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public bool? IsRecalculated { get; set; }
    }

    public class ViewResultManager : ViewModelBase,IDisposable
    {
        private static ViewResultManager _instance;
        private static readonly object _locker = new();
        public static ViewResultManager GetInstance() { lock (_locker) { _instance ??= new ViewResultManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\Spectromer\\Config\\";

        public static string SqliteDbPath { get; set; } = DirectoryPath + "Spectrum.db";

        public ViewResultManagerConfig Config { get; set; }

        public ObservableCollection<ViewResultSpectrum> ViewResluts { get; set; } = new ObservableCollection<ViewResultSpectrum>();
        public List<Scatter> ScatterPlots { get; set; } = new List<Scatter>();
        public List<Scatter> AbsoluteScatterPlots { get; set; } = new List<Scatter>();

        public int ViewReslutsSelectedIndex { get => _ViewReslutsSelectedIndex; set { _ViewReslutsSelectedIndex = value; OnPropertyChanged(); } }
        private int _ViewReslutsSelectedIndex = -1;
        public ListView ListView { get; set; }

        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand ViewReslutsClearCommand { get; set; }
        public RelayCommand QueryCommand { get; set; }
        public RelayCommand GenericQueryCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand DeleteAllCommand { get; set; }
        public RelayCommand ResetDatabaseCommand { get; set; }
        public RelayCommand ReloadCommand { get; set; }

        private SqlSugarClient _db;

        public ViewResultManager()
        {
            Config = ConfigService.Instance.GetRequiredService<ViewResultManagerConfig>();
            EditConfigCommand = new RelayCommand(a => EditConfig());
            ViewReslutsClearCommand = new RelayCommand(a => ViewReslutsClear());
            QueryCommand = new RelayCommand(a => Query());
            GenericQueryCommand = new RelayCommand(a => GenericQuery());
            SaveCommand = new RelayCommand(a => Save());
            DeleteAllCommand = new RelayCommand(a =>
            {
                if (MessageBox.Show("确定要删除数据库中所有记录吗？此操作不可恢复。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    DeleteAllRecords();
            });
            ResetDatabaseCommand = new RelayCommand(a =>
            {
                if (MessageBox.Show("确定要重置数据库吗？将删除数据库文件并重新创建。此操作不可恢复。", "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    ResetDatabase();
            });
            ReloadCommand = new RelayCommand(a => ReloadData());
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            // 确保表存在
            _db.CodeFirst.InitTables<SprectrumModel>();
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
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();
        }
        public void Query()
        {
            Query(null,null,Config.Count);
        }

        public void Delete(int index)
        {
            if (index >= 0 && index < ViewResluts.Count)
            {
                var item = ViewResluts[index];
                _db.Deleteable<SprectrumModel>().Where(x => x.Id == item.Id).ExecuteCommand();
                ViewResluts.RemoveAt(index);
            }
        }

        /// <summary>
        /// 删除选中的数据（同时从数据库删除）
        /// </summary>
        public void DeleteSelected(IList<ViewResultSpectrum> items)
        {
            foreach (var item in items.ToList())
            {
                _db.Deleteable<SprectrumModel>().Where(x => x.Id == item.Id).ExecuteCommand();
                ViewResluts.Remove(item);
            }
        }

        /// <summary>
        /// 删除数据库中所有记录并清空列表
        /// </summary>
        public void DeleteAllRecords()
        {
            _db.Deleteable<SprectrumModel>().ExecuteCommand();
            ViewReslutsClear();
        }

        /// <summary>
        /// 重置数据库（删除db文件并重新创建）
        /// </summary>
        public void ResetDatabase()
        {
            _db.Dispose();
            if (File.Exists(SqliteDbPath))
                File.Delete(SqliteDbPath);
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            _db.CodeFirst.InitTables<SprectrumModel>();
            ViewReslutsClear();
        }

        /// <summary>
        /// 重新加载数据
        /// </summary>
        public void ReloadData()
        {
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();
            LoadAll(Config.Count);
        }

        /// <summary>
        /// 更新数据库中EQE相关字段
        /// </summary>
        public void UpdateEqeFields(ViewResultSpectrum viewResult, bool isRecalculated = false)
        {
            _db.Updateable<SprectrumModel>()
                .SetColumns(x => x.EqeVoltage == viewResult.V)
                .SetColumns(x => x.EqeCurrentMA == viewResult.I)
                .SetColumns(x => x.Eqe == viewResult.Eqe)
                .SetColumns(x => x.LuminousFlux == viewResult.LuminousFlux)
                .SetColumns(x => x.RadiantFlux == viewResult.RadiantFlux)
                .SetColumns(x => x.LuminousEfficacy == viewResult.LuminousEfficacy)
                .SetColumns(x => x.ExcitationPurity == (double?)viewResult.ExcitationPurity)
                .SetColumns(x => x.IsRecalculated == isRecalculated)
                .Where(x => x.Id == viewResult.Id)
                .ExecuteCommand();
        }

        public void Save()
        {
            //if (ViewResluts.Count >0 &&  ViewReslutsSelectedIndex > -1)
            //{
            //    if (ViewResluts[ViewReslutsSelectedIndex] is KBItemMaster kbItemMaster)
            //    {
            //        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            //        string regexPattern = $"[{Regex.Escape(invalidChars)}]";
            //        string csvpath = Configs.SavePathCsv + $"\\{Regex.Replace(kbItemMaster.Model, regexPattern, "")}_{kbItemMaster.CreateTime:yyyyMMdd}.csv";
                    
            //        using var dialog = new System.Windows.Forms.SaveFileDialog();
            //        dialog.Filter = "CSV files (*.csv) | *.csv";
            //        dialog.FileName = csvpath;
            //        dialog.RestoreDirectory = true;
            //        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            //        kbItemMaster.SaveCsv(dialog.FileName);
            //    }
            //}

        }

        /// <summary>
        /// 初始化，从数据库读取数据到ViewResluts，count=-1为全部，否则仅取最新count条
        /// </summary>
        public void LoadAll(int count = 100)
        {
            ViewResluts.Clear();
            var query = _db.Queryable<SprectrumModel>().OrderBy(x => x.Id, Config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();
            foreach (var dbItem in dbList)
            {
                ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(dbItem);
                ViewResluts.Add(viewResultSpectrum);
                ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
                AbsoluteScatterPlots.Add(viewResultSpectrum.AbsoluteScatterPlot);
            }
        }

        public void Save(SprectrumModel item)
        {
            if (item == null) return;
            int id = _db.Insertable(item).ExecuteReturnIdentity();
            item.Id = id; // 更新ID
            ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(item);

            // Persist ExcitationPurity computed in Gen()
            _db.Updateable<SprectrumModel>()
                .SetColumns(x => x.ExcitationPurity == (double?)viewResultSpectrum.ExcitationPurity)
                .Where(x => x.Id == id)
                .ExecuteCommand();

            if (Config.OrderByType == OrderByType.Desc)
            {
                ScatterPlots.Insert(0,viewResultSpectrum.ScatterPlot);
                AbsoluteScatterPlots.Insert(0, viewResultSpectrum.AbsoluteScatterPlot);
                ViewResluts.Insert(0, viewResultSpectrum); //倒序插入

                if (Config.AutoRefresh)
                {
                    ViewReslutsSelectedIndex = 0;
                }
            }
            else
            {
                ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
                AbsoluteScatterPlots.Add(viewResultSpectrum.AbsoluteScatterPlot);
                ViewResluts.Add(viewResultSpectrum);
                if (Config.AutoRefresh)
                {
                    ViewReslutsSelectedIndex = ViewResluts.Count - 1;
                    ListView?.ScrollIntoView(item);
                }
            }

        }

        public void GenericQuery()
        {
            GenericQuery<SprectrumModel, ViewResultSpectrum> genericQuery = new GenericQuery<SprectrumModel, ViewResultSpectrum>(_db, ViewResluts,a=>new ViewResultSpectrum(a));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }

        /// <summary>
        /// 根据条件查询，举例：根据SN或Model等
        /// </summary>
        public void Query(string model = null, string sn = null, int count = -1)
        {
            ViewResluts.Clear();

            var query = _db.Queryable<SprectrumModel>();
            query = query.OrderBy(x => x.Id, Config.OrderByType);
            var dbList = count > 0 ? query.Take(count).ToList() : query.ToList();

            foreach (var dbItem in dbList)
            {
                ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(dbItem);
                ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
                AbsoluteScatterPlots.Add(viewResultSpectrum.AbsoluteScatterPlot);
                ViewResluts.Add(viewResultSpectrum);
            }
        }

        public void Dispose()
        {
            _db?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}