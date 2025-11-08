using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Cache;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public enum SpectrometerType
    {
        CMvSpectra = 0,
        LightModule = 1,
    }

    public class TextSectrumSNPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));

            combo.ItemsSource = CameraLicenseDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "lic_type", 1 } });
            combo.DisplayMemberPath = "MacAddress";
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }

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



        [PropertyEditorType(typeof(TextSectrumSNPropertiesEditor)), Category("Base")]
        public override string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN;

        [DisplayName("DeviceAutoConnect")]
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen;

        [DisplayName("WaveLengthFile")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; OnPropertyChanged(); } }
        private string _WavelengthFile;

        [DisplayName("AmplitudeFile")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; OnPropertyChanged(); } }
        private string _MaguideFile;

        [DisplayName("IsEnableNd")]
        public bool IsWithND { get => _IsWithND; set { _IsWithND = value; OnPropertyChanged(); } }
        private bool _IsWithND;
        [DisplayName("ConnectType")]
        public SpectrometerType SpectrometerType { get => _SpectrometerType; set { _SpectrometerType = value; OnPropertyChanged(); if (value == SpectrometerType.CMvSpectra) _ComPort = "0"; OnPropertyChanged(nameof(ComPort)); } }
        private SpectrometerType _SpectrometerType = SpectrometerType.CMvSpectra;

        public string ComPort { get => _ComPort; set { _ComPort = value; OnPropertyChanged(); } }
        private string _ComPort = "0";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [DisplayName("Saturation")]
        public int Saturation { get => _Saturation; set { _Saturation = value; OnPropertyChanged(); } }
        private int _Saturation = 80;


        [DisplayName("MaxIntegrationTime_Ms"), Category("ConfigerInfo")]
        public int MaxIntegralTime { get => _TimeLimit; set { _TimeLimit = value; OnPropertyChanged(); } }
        private int _TimeLimit = 60000;

        [DisplayName("AutoTestInterval_Ms"),Category("ConfigerInfo")]
        public int AutoTestTime { get => _AutoTestTime; set { _AutoTestTime = value; OnPropertyChanged(); } }
        private int _AutoTestTime = 100;

        [DisplayName("StartIntegrationTime_Ms"), Category("ConfigerInfo")]
        public float BeginIntegralTime { get => _TimeFrom; set { _TimeFrom = value; OnPropertyChanged(); } }
        private float _TimeFrom = 10;


        public bool IsAutoDark { get => _IsAutoDark; set { if (value) IsShutterEnable = false; _IsAutoDark = value; OnPropertyChanged(); } }
        private bool _IsAutoDark;

        public bool IsShutterEnable { get => _IsShutter; set { if (value) IsAutoDark = false; _IsShutter = value; OnPropertyChanged(); } }
        private bool _IsShutter;
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


    [DisplayName("SetEmissionSP100Config")]
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
