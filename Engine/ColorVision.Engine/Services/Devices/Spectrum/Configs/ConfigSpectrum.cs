using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Configs;
using ColorVision.Engine.Services.Core;
using ColorVision.UI.PropertyEditor;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Windows;

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

        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; NotifyPropertyChanged(); } }
        private string _WavelengthFile;
        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; NotifyPropertyChanged(); } }
        private string _MaguideFile;

        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen;

        /// <summary>
        /// 最大积分时间
        /// </summary>
        public int MaxIntegralTime { get => _TimeLimit; set { _TimeLimit = value; NotifyPropertyChanged(); } }
        private int _TimeLimit = 60000;

        /// <summary>
        /// 自动测试间隔
        /// </summary>
        public int AutoTestTime { get => _AutoTestTime; set { _AutoTestTime = value; NotifyPropertyChanged(); } }
        private int _AutoTestTime = 100;
        /// <summary>
        /// 起始积分时间
        /// </summary>
        public float BeginIntegralTime { get => _TimeFrom; set { _TimeFrom = value; NotifyPropertyChanged(); } }
        private float _TimeFrom = 10;

        public bool IsShutterEnable { get => _IsShutter; set { if (value) IsAutoDark = false; _IsShutter = value; NotifyPropertyChanged(); } } 
        private bool _IsShutter;
        public bool IsAutoDark { get => _IsAutoDark; set { if (value) IsShutterEnable = false; _IsAutoDark = value; NotifyPropertyChanged(); } }
        private bool _IsAutoDark;

        public ShutterConfig ShutterCfg { get => _ShutterCfg; set { _ShutterCfg = value; NotifyPropertyChanged(); } }
        private ShutterConfig _ShutterCfg;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        public SelfAdaptionInitDark SelfAdaptionInitDark { get; set; } = new SelfAdaptionInitDark();

        public SetEmissionSP100Config SetEmissionSP100Config { get; set; } = new SetEmissionSP100Config();
    }

    [DisplayName("EmissionSP100设置")]
    public class SetEmissionSP100Config : ViewModelBase
    {

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled = true;

        public int nStartPos { get => _nStartPos; set { _nStartPos = value; NotifyPropertyChanged(); } }
        private int _nStartPos = 1691;

        public int nEndPos { get => _nEndPos; set { _nEndPos = value; NotifyPropertyChanged(); } }
        private int _nEndPos = 2048;

        public double dMeanThreshold { get => _dMeanThreshold; set { _dMeanThreshold = value; NotifyPropertyChanged(); } }
        private double _dMeanThreshold = 80;
    }
}
