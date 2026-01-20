using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Cache;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public enum SpectrometerType
    {
        CMvSpectra = 0,
        LightModule = 1,
    }
    public class BoolToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = (bool)value;
            // If parameter is "Inverse", flip the logic
            if (parameter != null && parameter.ToString() == "Inverse")
            {
                isVisible = !isVisible;
            }

            return isVisible ? double.NaN : 0.0; // double.NaN is equivalent to "Auto"
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
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

            combo.ItemsSource = PhyLicenseDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "lic_type", 1 } });
            combo.DisplayMemberPath = "MacAddress";
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }

    public class ConfigSpectrum : DeviceServiceConfig, IServiceConfig, IFileServerCfg
    {
        public ConfigSpectrum()
        {

        }
      

        [PropertyEditorType(typeof(TextSectrumSNPropertiesEditor))]
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

        [DisplayName("BaudRate"), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [DisplayName("Saturation")]
        public int Saturation { get => _Saturation; set { _Saturation = value; OnPropertyChanged(); } }
        private int _Saturation = 80;

        [DisplayName("MaxIntegrationTime_Ms")]
        public int MaxIntegralTime { get => _TimeLimit; set { _TimeLimit = value; OnPropertyChanged(); } }
        private int _TimeLimit = 60000;

        [DisplayName("AutoTestInterval_Ms")]
        public int AutoTestTime { get => _AutoTestTime; set { _AutoTestTime = value; OnPropertyChanged(); } }
        private int _AutoTestTime = 100;

        [DisplayName("StartIntegrationTime_Ms")]
        public float BeginIntegralTime { get => _TimeFrom; set { _TimeFrom = value; OnPropertyChanged(); } }
        private float _TimeFrom = 10;


        public bool IsAutoDark { get => _IsAutoDark; set { if (value) IsShutter = false; _IsAutoDark = value; OnPropertyChanged(); } }
        private bool _IsAutoDark;

        public SelfAdaptionInitDark SelfAdaptionInitDark { get; set; } = new SelfAdaptionInitDark();

        public SetEmissionSP100Config SetEmissionSP100Config { get; set; } = new SetEmissionSP100Config();

        public NDConfig NDConfig { get; set; } = new NDConfig();

        public bool IsShutter { get => _IsShutter; set { if (value) IsAutoDark = false; _IsShutter = value; OnPropertyChanged(); } }
        private bool _IsShutter;

        public ShutterConfig ShutterCfg { get => _ShutterCfg; set { _ShutterCfg = value; OnPropertyChanged(); } }
        private ShutterConfig _ShutterCfg = new ShutterConfig();

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        public GetDataConfig GetDataConfig { get; set; } = new GetDataConfig(); 

    }

    public class GetDataConfig : ViewModelBase, IConfig
    {
        [DisplayName("IsSyncFrequencyEnabled")]
        public bool IsSyncFrequencyEnabled { get => _IsSyncFrequencyEnabled; set { _IsSyncFrequencyEnabled = value; OnPropertyChanged(); } }
        private bool _IsSyncFrequencyEnabled;

        [DisplayName("Syncfreq")]
        public double Syncfreq { get => _Syncfreq; set { _Syncfreq = value; OnPropertyChanged(); } }
        private double _Syncfreq = 1000;

        [DisplayName("SyncfreqFactor")]
        public int SyncfreqFactor { get => _SyncfreqFactor; set { _SyncfreqFactor = value; OnPropertyChanged(); } }
        private int _SyncfreqFactor = 10;

        [DisplayName("FilterBW")]
        public int FilterBW { get => _FilterBW; set { _FilterBW = value; OnPropertyChanged(); } }
        private int _FilterBW = 5;

        public float SetWL1 { get => _SetWL1; set { _SetWL1 = value; OnPropertyChanged(); } }
        private float _SetWL1 = 380;
        public float SetWL2 { get => _SetWL2; set { _SetWL2 = value; OnPropertyChanged(); } }
        private float _SetWL2 = 780;

    }

    public class NDConfig : ViewModelBase
    {
        public bool IsNDPort { get => _IsNDPort; set { _IsNDPort = value; OnPropertyChanged(); } }
        private bool _IsNDPort;

        public bool IsBingNDDevice { get => _IsBingNDDevice; set { _IsBingNDDevice = value; OnPropertyChanged(); } }
        private bool _IsBingNDDevice = true;


        [PropertyEditorType(typeof(TextCFWPropertiesEditor)), PropertyVisibility(nameof(IsBingNDDevice))]
        public string NDBindDeviceCode { get => _NDBindDeviceCode; set { _NDBindDeviceCode = value; OnPropertyChanged(); } }
        private string _NDBindDeviceCode;

        [PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), PropertyVisibility(nameof(IsBingNDDevice),true)]
        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        [PropertyEditorType(typeof(TextBaudRatePropertiesEditor)), PropertyVisibility(nameof(IsBingNDDevice), true)]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 115200;

        public bool EnableResetND { get => _EnableResetND; set { _EnableResetND = value; OnPropertyChanged(); } }
        private bool _EnableResetND;

        public double NDMaxExpTime { get => _NDMaxExpTime; set { _NDMaxExpTime = value; OnPropertyChanged(); } }
        private double _NDMaxExpTime;

        public double NDMinExpTime { get => _NDMinExpTime; set { _NDMinExpTime = value; OnPropertyChanged(); } }
        private double _NDMinExpTime;

        public List<int> NDRate { get; set; } = new List<int>();

        public List<string> NDCaliNameGroups { get; set; } = new List<string>();

        [DisplayName("黑暗校零ND配置")]
        public int DarkNDPort { get => _DarkNDPort; set { _DarkNDPort = value; OnPropertyChanged(); } }
        private int _DarkNDPort = -1;
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
