using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using ProjectKB.Auth;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ProjectKB
{
    public class Summary : ViewModelBase
    {
        [DisplayName("启用ShopFloor"), Category("KB")]
        public bool UseMes { get => _UseMesh; set { _UseMesh = value; OnPropertyChanged(); } }
        private bool _UseMesh = true;

        [DisplayName("自动上传SN"), Category("KB")]
        public bool AutoUploadSN { get => _AutoUploadSN; set { _AutoUploadSN = value; OnPropertyChanged(); } }
        private bool _AutoUploadSN;

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

        /// <summary>
        /// 目标生产
        /// </summary>
        [DisplayName("目标生产")]
        public int TargetProduction { get => _TargetProduction; set { _TargetProduction = value; OnPropertyChanged(); } }
        private int _TargetProduction;

    }

    public class SummaryManager
    {
        private static SummaryManager _instance;
        private static readonly object _locker = new();
        public static SummaryManager GetInstance() { lock (_locker) { _instance ??= new SummaryManager(); return _instance; } }
        public RelayCommand EditCommand { get; set; }
        public RelayCommand OpenStatisticsCommand { get; set; }

        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string ConfigPath { get; set; } = DirectoryPath + "ProjectKBSummary.json";


        public Summary Summary { get; set; } = new Summary();
        public SummaryManager()
        {
            EditCommand = new RelayCommand(a => Edit());
            OpenStatisticsCommand = new RelayCommand(a => OpenStatistics());

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
            if (!KBAuthManager.GetInstance().RequireAdmin(Application.Current.GetActiveWindow())) return;

            new PropertyEditorWindow(Summary) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner }.ShowDialog();
            this.Save();
        }

        public static void OpenStatistics()
        {
            Window? owner = Application.Current.GetActiveWindow();
            new KBProductionStatisticsWindow
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            }.Show();
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
