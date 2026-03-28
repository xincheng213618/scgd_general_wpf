using ColorVision.Common.MVVM;
using ColorVision.UI;
using Spectrum.PropertyEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Spectrum.Configs
{
    public class ShutterConfig : ViewModelBase
    {
        [DisplayName("Serial"), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SzComName { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr = "COM1";

        [DisplayName("BaudRate"), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        public string OpenCmd { get => _OpenCmd; set { _OpenCmd = value; OnPropertyChanged(); } }
        private string _OpenCmd = "a";
        public string CloseCmd { get => _CloseCmd; set { _CloseCmd = value; OnPropertyChanged(); } }
        private string _CloseCmd = "b";

        [DisplayName("DelayMs")]
        public int DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private int _DelayTime = 1000;
    }

    public class SpectrumConfig : ViewModelBase, IConfig
    {
        public static SpectrumConfig Instance => ConfigService.Instance.GetRequiredService<SpectrumConfig>();



        public SpectrometerType SpectrometerType { get => _SpectrometerType; set { _SpectrometerType = value; OnPropertyChanged(); } }
        private SpectrometerType _SpectrometerType = SpectrometerType.CMvSpectra;

        public bool IsComPort { get => _IsComPort; set { _IsComPort = value; OnPropertyChanged(); } }   
        private bool _IsComPort;

        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;



        public ShutterConfig ShutterConfig { get => _ShutterConfig; set { _ShutterConfig = value; OnPropertyChanged(); } }
        private ShutterConfig _ShutterConfig = new ShutterConfig();
        public NDConfig NDConfig { get => _NDConfig; set { _NDConfig = value; OnPropertyChanged(); } }
        private NDConfig _NDConfig = new NDConfig();

        public FilterWheelConfig FilterWheelConfig { get => _FilterWheelConfig; set { _FilterWheelConfig = value; OnPropertyChanged(); } }
        private FilterWheelConfig _FilterWheelConfig = new FilterWheelConfig();
    }

    public class NDConfig : ViewModelBase, IConfig
    {
        public bool IsNDPort { get => _IsNDPort; set { _IsNDPort = value; OnPropertyChanged(); } }
        private bool _IsNDPort;

        [PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        [PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        public bool EnableResetND { get => _EnableResetND; set { _EnableResetND = value; OnPropertyChanged(); } }
        private bool _EnableResetND;

        public double NDMaxExpTime { get => _NDMaxExpTime; set { _NDMaxExpTime = value; OnPropertyChanged(); } }
        private double _NDMaxExpTime;

        public double NDMinExpTime { get => _NDMinExpTime; set { _NDMinExpTime = value; OnPropertyChanged(); } }
        private double _NDMinExpTime;

        public List<int> NDRate { get; set; } = new List<int>();

        public List<string> NDCaliNameGroups { get; set; } = new List<string>();

        [DisplayName("DarkNDPort")]
        public int DarkNDPort { get => _DarkNDPort; set { _DarkNDPort = value; OnPropertyChanged(); } }
        private int _DarkNDPort = -1;
    }
}
