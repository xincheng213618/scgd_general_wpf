using ColorVision.Common.MVVM;
using ColorVision.Engine.Properties;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Utilities;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.LightingController
{
    public class ConfigLightingController : DeviceServiceConfig
    {
        [Category("Connection"), LocalizedDisplayName(nameof(Resources.AutoConnect))]
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen = true;

        [Category("Connection"), LocalizedDisplayName(nameof(Resources.SzComName)), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SzComName { get => _SzComName; set { _SzComName = value; OnPropertyChanged(); } }
        private string _SzComName = "COM1";

        [Category("Connection"), LocalizedDisplayName(nameof(Resources.BaudRate)), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [Category("Communication"), LocalizedDisplayName(nameof(Resources.Timeout))]
        public int Timeout { get => _Timeout; set { _Timeout = value; OnPropertyChanged(); } }
        private int _Timeout = 5000;

        [Category("Communication"), LocalizedDisplayName(nameof(Resources.RetryCount))]
        public int RetryCount { get => _RetryCount; set { _RetryCount = value; OnPropertyChanged(); } }
        private int _RetryCount = 3;

        [Category("Communication"), LocalizedDisplayName(nameof(Resources.ChannelCount))]
        public int Ports { get => _Ports; set { _Ports = value; OnPropertyChanged(); } }
        private int _Ports = 6;

        [Category("Communication"), DisplayName("Delay")]
        public int Delay { get => _Delay; set { _Delay = value; OnPropertyChanged(); } }
        private int _Delay;

        [Category("Channel"), DisplayName("Channel A")]
        public PMChannelConfig CHA { get => _CHA; set { _CHA = value; OnPropertyChanged(); OnPropertyChanged(nameof(Channels)); } }
        private PMChannelConfig _CHA = new("A", "A");

        [Category("Channel"), DisplayName("Channel B")]
        public PMChannelConfig CHB { get => _CHB; set { _CHB = value; OnPropertyChanged(); OnPropertyChanged(nameof(Channels)); } }
        private PMChannelConfig _CHB = new("B", "B");

        [Category("Communication"), DisplayName("Command Format"), Description("0 is the channel code and 1 is the channel value.")]
        public string CommandFormat { get => _CommandFormat; set { _CommandFormat = value; OnPropertyChanged(); } }
        private string _CommandFormat = "S{0}{1:D4}#";

        [Browsable(false), JsonIgnore]
        public IEnumerable<PMChannelConfig> Channels => [CHA, CHB];
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class PMChannelConfig : ViewModelBase
    {
        public PMChannelConfig()
        {
        }

        public PMChannelConfig(string code, string name)
        {
            Code = code;
            Name = name;
        }

        [DisplayName("Code")]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code = string.Empty;

        [DisplayName("Name")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

        [DisplayName("On Value")]
        public int OnValue { get => _OnValue; set { _OnValue = value; OnPropertyChanged(); } }
        private int _OnValue = 255;

        [DisplayName("Off Value")]
        public int OffValue { get => _OffValue; set { _OffValue = value; OnPropertyChanged(); } }
        private int _OffValue;

        [Browsable(false), JsonIgnore]
        public int Value { get => _Value; set { _Value = value; OnPropertyChanged(); } }
        private int _Value;

        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Code : $"{Name} ({Code})";
    }
}
