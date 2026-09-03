using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Cache;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
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
        Gaolitong = 2,
    }

    public class SpectrumCalibrationGroup : ViewModelBase
    {
        [DisplayName("SpectrumGroupName")]
        public string GroupName { get => _GroupName; set { _GroupName = value; OnPropertyChanged(); } }
        private string _GroupName = "Default";

        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [DisplayName("WaveLengthFile")]
        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; OnPropertyChanged(); } }
        private string _WavelengthFile = "WavaLength.dat";

        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [DisplayName("AmplitudeFile")]
        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; OnPropertyChanged(); } }
        private string _MaguideFile = "Magiude.dat";

        [DisplayName("SpectrumNdIndex")]
        public int NDHoleIndex { get => _NDHoleIndex; set { _NDHoleIndex = value; OnPropertyChanged(); } }
        private int _NDHoleIndex = -1;
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

            Button button = new Button
            {
                Content = ColorVision.Engine.Properties.Resources.Edit,
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 70,
            };

            RelayCommand relayCommand = new RelayCommand((o) =>
            {
                LicenseManagerWindow licenseManagerWindow = new LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                licenseManagerWindow.ShowDialog();
            });

            button.Command = relayCommand;
            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);


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

    [DisplayName("SpectrumSettingsTitle")]
    public class ConfigSpectrum : DeviceServiceConfig, IFileServerCfg
    {
        public ConfigSpectrum()
        {

        }


        [PropertyEditorType(typeof(TextSectrumSNPropertiesEditor))]
        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumSN")]
        public override string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN;


        [DisplayName("DeviceAutoConnect")]
        [Category("SpectrumDeviceConnection")]
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen;

        [DisplayName("WaveLengthFile")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [Category("SpectrumCalibrationCorrection")]
        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; OnPropertyChanged(); } }
        private string _WavelengthFile;

        [DisplayName("AmplitudeFile")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [Category("SpectrumCalibrationCorrection")]
        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; OnPropertyChanged(); } }
        private string _MaguideFile;

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("CalibrationGroup")]
        [Description("SpectrumCalibrationFileSettingsHint")]
        public ObservableCollection<SpectrumCalibrationGroup> CalibrationGroups
        {
            get => _CalibrationGroups;
            set { _CalibrationGroups = value ?? new ObservableCollection<SpectrumCalibrationGroup>(); OnPropertyChanged(); OnPropertyChanged(nameof(ActiveCalibrationGroup)); }
        }
        private ObservableCollection<SpectrumCalibrationGroup> _CalibrationGroups = new ObservableCollection<SpectrumCalibrationGroup>();

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("SpectrumActiveGroup")]
        public string ActiveCalibrationGroupName { get => _ActiveCalibrationGroupName; set { _ActiveCalibrationGroupName = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveCalibrationGroup)); } }
        private string _ActiveCalibrationGroupName = "Default";

        [Browsable(false)]
        [JsonIgnore]
        public SpectrumCalibrationGroup ActiveCalibrationGroup
        {
            get
            {
                EnsureCalibrationGroups();
                return CalibrationGroups.FirstOrDefault(a => string.Equals(a.GroupName, ActiveCalibrationGroupName, StringComparison.OrdinalIgnoreCase))
                    ?? CalibrationGroups.First();
            }
        }

        public void EnsureCalibrationGroups()
        {
            if (CalibrationGroups.Count == 0)
            {
                var group = new SpectrumCalibrationGroup { GroupName = "Default" };
                if (!string.IsNullOrWhiteSpace(WavelengthFile))
                    group.WavelengthFile = WavelengthFile;
                if (!string.IsNullOrWhiteSpace(MaguideFile))
                    group.MaguideFile = MaguideFile;
                CalibrationGroups.Add(group);
            }

            if (string.IsNullOrWhiteSpace(ActiveCalibrationGroupName) || CalibrationGroups.All(a => !string.Equals(a.GroupName, ActiveCalibrationGroupName, StringComparison.OrdinalIgnoreCase)))
                ActiveCalibrationGroupName = CalibrationGroups.First().GroupName;
        }

        public SpectrumCalibrationGroup? FindCalibrationGroupForND(int holeIndex, string? holeName)
        {
            EnsureCalibrationGroups();
            SpectrumCalibrationGroup? group = CalibrationGroups.FirstOrDefault(a => a.NDHoleIndex == holeIndex);
            if (group != null)
                return group;

            if (string.IsNullOrWhiteSpace(holeName))
                return null;

            return CalibrationGroups.FirstOrDefault(a => string.Equals(a.GroupName, holeName, StringComparison.OrdinalIgnoreCase));
        }



        [DisplayName("ConnectType")]
        [Category("SpectrumDeviceConnection")]
        public SpectrometerType SpectrometerType { get => _SpectrometerType; set { _SpectrometerType = value; OnPropertyChanged();} }
        private SpectrometerType _SpectrometerType = SpectrometerType.CMvSpectra;

        [PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), DisplayName("SpectrumComPort")]
        [Category("SpectrumDeviceConnection")]
        public string ComPortView
        {
            get => "COM" + ComPort;
            set
            {
                // 1. 处理输入值，去掉 "COM"
                string newPortValue = value;
                if (!string.IsNullOrEmpty(value) && value.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                {
                    newPortValue = value.Substring(3);
                }

                // 2. 【关键】在赋值给 ComPort 之前，先判断值是否真的变了
                // 如果当前 ComPort 已经是 "1"，而你又传入 "1"，就直接 return，切断循环
                if (ComPort != newPortValue)
                {
                    ComPort = newPortValue;
                    OnPropertyChanged(); // 通知 ComPortView 变了
                }
            }
        }
        [Browsable(false)]
        public string ComPort { get => _ComPort; set { _ComPort = value; OnPropertyChanged(); OnPropertyChanged(nameof(ComPortView)); } }
        private string _ComPort = "0";


        [DisplayName("BaudRate"), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        [Category("SpectrumDeviceConnection")]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [DisplayName("Saturation")]
        [Category("SpectrumAcquisitionDisplay")]
        public int Saturation { get => _Saturation; set { _Saturation = value; OnPropertyChanged(); } }
        private int _Saturation = 80;

        [DisplayName("MaxIntegrationTime_Ms")]
        [Category("SpectrumAcquisitionDisplay")]
        public int MaxIntegralTime { get => _TimeLimit; set { _TimeLimit = value; OnPropertyChanged(); } }
        private int _TimeLimit = 60000;

        [DisplayName("AutoTestInterval_Ms")]
        [Category("SpectrumAcquisitionDisplay")]
        public int AutoTestTime { get => _AutoTestTime; set { _AutoTestTime = value; OnPropertyChanged(); } }
        private int _AutoTestTime = 100;

        [DisplayName("StartIntegrationTime_Ms")]
        [Category("SpectrumAcquisitionDisplay")]
        public float BeginIntegralTime { get => _TimeFrom; set { _TimeFrom = value; OnPropertyChanged(); } }
        private float _TimeFrom = 10;

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumShutterEnabled")]
        public bool IsShutterEnable { get => _IsShutterEnable; set { _IsShutterEnable = value; OnPropertyChanged(); } }
        private bool _IsShutterEnable;

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumShutterSettings")]
        public ShutterConfig ShutterCfg { get => _ShutterCfg; set { _ShutterCfg = value; OnPropertyChanged(); } }
        private ShutterConfig _ShutterCfg = new ShutterConfig();

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("SpectrumDarkSettings")]
        public SelfAdaptionInitDark SelfAdaptionInitDark { get; set; } = new SelfAdaptionInitDark();

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("EmissionSP100Set")]
        public SetEmissionSP100Config SetEmissionSP100Config { get; set; } = new SetEmissionSP100Config();

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdSettings")]
        public NDConfig NDConfig { get; set; } = new NDConfig();

        [Category("SpectrumAcquisitionDisplay")]
        [DisplayName("SpectrumFileSettings")]
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        [Category("SpectrumAcquisitionDisplay")]
        [DisplayName("SpectrumAcquisitionSettings")]
        public GetDataConfig GetDataConfig { get; set; } = new GetDataConfig();

    }

    public class GetDataConfig : ViewModelBase, IConfig
    {
        [DisplayName("IsSyncFrequencyEnabled")]
        [Category("SpectrumAcquisitionDisplay")]
        public bool IsSyncFrequencyEnabled { get => _IsSyncFrequencyEnabled; set { _IsSyncFrequencyEnabled = value; OnPropertyChanged(); } }
        private bool _IsSyncFrequencyEnabled;

        [DisplayName("Syncfreq")]
        [Category("SpectrumAcquisitionDisplay")]
        public double Syncfreq { get => _Syncfreq; set { _Syncfreq = value; OnPropertyChanged(); } }
        private double _Syncfreq = 1000;

        [DisplayName("SyncfreqFactor")]
        [Category("SpectrumAcquisitionDisplay")]
        public int SyncfreqFactor { get => _SyncfreqFactor; set { _SyncfreqFactor = value; OnPropertyChanged(); } }
        private int _SyncfreqFactor = 10;

        [DisplayName("FilterBW")]
        [Category("SpectrumAcquisitionDisplay")]
        public int FilterBW { get => _FilterBW; set { _FilterBW = value; OnPropertyChanged(); } }
        private int _FilterBW = 5;

        [Category("SpectrumAcquisitionDisplay")]
        [DisplayName("SpectrumStartWavelength")]
        public float SetWL1 { get => _SetWL1; set { _SetWL1 = value; OnPropertyChanged(); } }
        private float _SetWL1 = 380;
        [Category("SpectrumAcquisitionDisplay")]
        [DisplayName("SpectrumEndWavelength")]
        public float SetWL2 { get => _SetWL2; set { _SetWL2 = value; OnPropertyChanged(); } }
        private float _SetWL2 = 780;

    }

    public class NDConfig : ViewModelBase
    {
        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdEnabled")]
        public bool IsNDPort { get => _IsNDPort; set { _IsNDPort = value; OnPropertyChanged(); } }
        private bool _IsNDPort;

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdBind")]
        public bool IsBingNDDevice { get => _IsBingNDDevice; set { _IsBingNDDevice = value; OnPropertyChanged(); } }
        private bool _IsBingNDDevice = true;


        [PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceCfwPort)),PropertyVisibility(nameof(IsBingNDDevice))]
        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdDevice")]
        public string NDBindDeviceCode { get => _NDBindDeviceCode; set { _NDBindDeviceCode = value; OnPropertyChanged(); } }
        private string _NDBindDeviceCode;

        [PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), PropertyVisibility(nameof(IsBingNDDevice),true)]
        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumComPort")]
        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        [PropertyEditorType(typeof(TextBaudRatePropertiesEditor)), PropertyVisibility(nameof(IsBingNDDevice), true)]
        [Category("SpectrumDeviceConnection")]
        [DisplayName("BaudRate")]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdReset")]
        public bool EnableResetND { get => _EnableResetND; set { _EnableResetND = value; OnPropertyChanged(); } }
        private bool _EnableResetND;

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdMaxTime")]
        public double NDMaxExpTime { get => _NDMaxExpTime; set { _NDMaxExpTime = value; OnPropertyChanged(); } }
        private double _NDMaxExpTime;

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdMinTime")]
        public double NDMinExpTime { get => _NDMinExpTime; set { _NDMinExpTime = value; OnPropertyChanged(); } }
        private double _NDMinExpTime;

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdRates")]
        public List<int> NDRate { get; set; } = new List<int>();

        [Category("SpectrumDeviceConnection")]
        [DisplayName("SpectrumNdGroups")]
        public List<string> NDCaliNameGroups { get; set; } = new List<string>();

        [DisplayName("DarkNDPort")]
        [Category("SpectrumDeviceConnection")]
        public int DarkNDPort { get => _DarkNDPort; set { _DarkNDPort = value; OnPropertyChanged(); } }
        private int _DarkNDPort = -1;
    }


    [DisplayName("EmissionSP100Set")]
    public class SetEmissionSP100Config : ViewModelBase
    {

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("SpectrumSp100Enabled")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("SpectrumSp100Start")]
        public int nStartPos { get => _nStartPos; set { _nStartPos = value; OnPropertyChanged(); } }
        private int _nStartPos = 1691;

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("SpectrumSp100End")]
        public int nEndPos { get => _nEndPos; set { _nEndPos = value; OnPropertyChanged(); } }
        private int _nEndPos = 2048;

        [Category("SpectrumCalibrationCorrection")]
        [DisplayName("SpectrumSp100Threshold")]
        public double dMeanThreshold { get => _dMeanThreshold; set { _dMeanThreshold = value; OnPropertyChanged(); } }
        private double _dMeanThreshold = 80;
    }
}
