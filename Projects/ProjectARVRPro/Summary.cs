using ColorVision.Common.MVVM;
using ColorVision.Common;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public class Summary : ViewModelBase
    {

        /// <summary>
        /// 线别
        /// </summary>
        [DisplayName("站别")]
        public string Stage { get => _Stage; set { _Stage = value; OnPropertyChanged(); } }
        private string _Stage = "F100";

        /// <summary>
        /// 线别
        /// </summary>
        [DisplayName("线别")]
        public string LineNO { get => _LineNO; set { _LineNO = value; OnPropertyChanged(); } }
        private string _LineNO = string.Empty;
        /// <summary>
        /// 工号
        /// </summary>
        [DisplayName("工号")]
        public string WorkerNO { get => _WorkerNO; set { _WorkerNO = value; OnPropertyChanged(); } }
        private string _WorkerNO = string.Empty;

        [DisplayName("Opno")]
        public string Opno { get => _Opno; set { _Opno = value; OnPropertyChanged(); } }
        private string _Opno = string.Empty;

        
        [DisplayName("设备号")]
        public string MachineNO { get => _MachineNO; set { _MachineNO = value; OnPropertyChanged(); } }
        private string _MachineNO = string.Empty;

        [DisplayName("是否显示总结信息")]
        public bool IsShowSummary { get => _IsShowSummary; set { _IsShowSummary = value; OnPropertyChanged(); } }
        private bool _IsShowSummary;
        public double Width { get => _Width; set { _Width = value; OnPropertyChanged(); } }
        private double _Width = 300;
        /// <summary>
        /// 目标生产
        /// </summary>
        [DisplayName("目标生产")]
        public int TargetProduction { get => _TargetProduction; set { _TargetProduction = value; OnPropertyChanged(); } }
        private int _TargetProduction;

        /// <summary>
        /// 已生产
        /// </summary>
        [DisplayName("已生产")]
        public int ActualProduction { get => _ActualProduction; set { _ActualProduction = value; OnPropertyChanged(); } }
        private int _ActualProduction;

        [DisplayName("良品数量")]
        public int GoodProductCount { get => _GoodProductCount; set { _GoodProductCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(GoodProductRate)); } }
        private int _GoodProductCount;

        /// <summary>
        /// 不良品数量
        /// </summary>
        [DisplayName("不良品数量")]
        public int DefectiveProductCount { get => _DefectiveProductCount; set { _DefectiveProductCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(DefectiveProductRate)); } }
        private int _DefectiveProductCount;

        /// <summary>
        /// 良品率
        /// </summary>
        [JsonIgnore]
        [DisplayName("良品率")]
        [Browsable(false)]
        public double GoodProductRate { get => ActualProduction > 0 ? GoodProductCount / (double)ActualProduction : 0; }

        /// <summary>
        /// 不良率
        /// </summary>
        [JsonIgnore]
        [Browsable(false)]
        public double DefectiveProductRate { get => ActualProduction > 0 ? DefectiveProductCount / (double)ActualProduction : 0; }
    }

    public class SummaryManager
    {
        private static SummaryManager _instance;
        private static readonly object _locker = new();
        public static SummaryManager GetInstance() { lock (_locker) { _instance ??= new SummaryManager(); return _instance; } }
        public RelayCommand EditCommand { get; set; }

        public static string DirectoryPath { get; set; } = ColorVisionPaths.ConfigDirectory;

        public static string ConfigPath { get; set; } = Path.Combine(DirectoryPath, "ProjectARVRLiteSummary.json");


        public Summary Summary { get; set; } = new Summary();
        public SummaryManager()
        {
            EditCommand = new RelayCommand(a => Edit());

            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(ConfigPath) is Summary fix)
            {
                Summary = fix;
            }
            else
            {
                Save();
            }
        }

        public void Edit()
        {
            new PropertyEditorWindow(Summary) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner }.ShowDialog();
            this.Save();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);

                string json = JsonConvert.SerializeObject(Summary, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Optionally log or rethrow
            }
        }

        public static Summary? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<Summary> (json);
            }
            catch
            {
                return null;
            }
        }
    }


}
