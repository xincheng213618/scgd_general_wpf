using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{

    public class ConfigSpectrum : DeviceServiceConfig, IServiceConfig, IFileServerCfg
    {
        [JsonIgnore]
        public RelayCommand SetWavelengthFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand SetMaguideFileCommand { get; set; }

        public ConfigSpectrum()
        {
            SetWavelengthFileCommand = new RelayCommand(a => SetWavelengthFile());
            SetMaguideFileCommand = new RelayCommand(a => SetMaguideFile());
        }
        public void SetWavelengthFile()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "All Files|*.*"; // Optionally set a filter for file types
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    WavelengthFile = dialog.FileName;
                }
            }
        }

        public void SetMaguideFile()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "All Files|*.*"; // Optionally set a filter for file types
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MaguideFile = dialog.FileName;
                }
            }
        }

        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; OnPropertyChanged(); } }
        private string _WavelengthFile;

        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; OnPropertyChanged(); } }
        private string _MaguideFile;

        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen;

        /// <summary>
        /// 最大积分时间
        /// </summary>
        public int MaxIntegralTime { get => _TimeLimit; set { _TimeLimit = value; OnPropertyChanged(); } }
        private int _TimeLimit = 60000;

        /// <summary>
        /// 自动测试间隔
        /// </summary>
        public int AutoTestTime { get => _AutoTestTime; set { _AutoTestTime = value; OnPropertyChanged(); } }
        private int _AutoTestTime = 100;
        /// <summary>
        /// 起始积分时间
        /// </summary>
        public float BeginIntegralTime { get => _TimeFrom; set { _TimeFrom = value; OnPropertyChanged(); } }
        private float _TimeFrom = 10;

        public bool IsShutterEnable { get => _IsShutter; set { if (value) IsAutoDark = false; _IsShutter = value; OnPropertyChanged(); } } 
        private bool _IsShutter;
        public bool IsAutoDark { get => _IsAutoDark; set { if (value) IsShutterEnable = false; _IsAutoDark = value; OnPropertyChanged(); } }
        private bool _IsAutoDark;

        [DisplayName("饱和度")]
        public int Saturation { get => _Saturation; set { _Saturation = value; OnPropertyChanged(); } }
        private int _Saturation = 80;

        public ShutterConfig ShutterCfg { get => _ShutterCfg; set { _ShutterCfg = value; OnPropertyChanged(); } }
        private ShutterConfig _ShutterCfg;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        public SelfAdaptionInitDark SelfAdaptionInitDark { get; set; } = new SelfAdaptionInitDark();

        public SetEmissionSP100Config SetEmissionSP100Config { get; set; } = new SetEmissionSP100Config();

        public NDConfig NDConfig { get; set; } = new NDConfig();
    }



    public class NDConfig : ViewModelBase
    {
        public bool IsNDPort { get => _IsNDPort; set { _IsNDPort = value; OnPropertyChanged(); } }
        private bool _IsNDPort;

        [PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        [PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 115200;

        public double NDMaxExpTime { get => _NDMaxExpTime; set { _NDMaxExpTime = value; OnPropertyChanged(); } }
        private double _NDMaxExpTime;

        public double NDMinExpTime { get => _NDMinExpTime; set { _NDMinExpTime = value; OnPropertyChanged(); } }
        private double _NDMinExpTime;

        public List<int> NDRate { get; set; } = new List<int>();

        public List<string> NDCaliNameGroups { get; set; } = new List<string>();
    }


    [DisplayName("EmissionSP100设置")]
    public class SetEmissionSP100Config : ViewModelBase
    {

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        public int nStartPos { get => _nStartPos; set { _nStartPos = value; OnPropertyChanged(); } }
        private int _nStartPos = 1691;

        public int nEndPos { get => _nEndPos; set { _nEndPos = value; OnPropertyChanged(); } }
        private int _nEndPos = 2048;

        public double dMeanThreshold { get => _dMeanThreshold; set { _dMeanThreshold = value; OnPropertyChanged(); } }
        private double _dMeanThreshold = 80;
    }
}
