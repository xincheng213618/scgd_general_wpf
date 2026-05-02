using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRPro.Process
{
    public enum PictureSwitchMode
    {
        [Description("雷鸟")]
        Thunderbird
    }

    public class PictureSwitchPreset
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string DisplayText => $"{Index}. {Command} - {Name}";
    }

    public class PictureSwitchConfig : ViewModelBase
    {
        public static IReadOnlyList<PictureSwitchPreset> Presets { get; } = new List<PictureSwitchPreset>
        {
            new() { Index = 1, Name = "十字对位图", Command = "PIC1" },
            new() { Index = 2, Name = "540x280_Solid_W51_FOV", Command = "PIC2" },
            new() { Index = 3, Name = "亮度和亮度均一性", Command = "PIC3" },
            new() { Index = 4, Name = "MTF_Nf_H", Command = "PIC4" },
            new() { Index = 5, Name = "MTF_Nf_V", Command = "PIC5" },
            new() { Index = 6, Name = "MTF_0.5Nf_H", Command = "PIC6" },
            new() { Index = 7, Name = "MTF_0.5Nf_V", Command = "PIC7" },
            new() { Index = 8, Name = "MTF_0.25Nf_H", Command = "PIC8" },
            new() { Index = 9, Name = "MTF_0.25Nf_V", Command = "PIC9" },
            new() { Index = 10, Name = "TV畸变", Command = "PICA" },
            new() { Index = 11, Name = "MTF_1NF", Command = "PICB" },
            new() { Index = 12, Name = "MTF_0.5NF", Command = "PICC" },
            new() { Index = 13, Name = "MTF_0.25NF", Command = "PICD" }
        };

        [DisplayName("启用切图")]
        public bool IsEnabled { get => _isEnabled; set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } } }
        private bool _isEnabled;

        [DisplayName("切图模式")]
        public PictureSwitchMode Mode { get => _mode; set { if (_mode != value) { _mode = value; OnPropertyChanged(); } } }
        private PictureSwitchMode _mode = PictureSwitchMode.Thunderbird;

        [DisplayName("发送值")]
        public string SendCommand { get => _sendCommand; set { if (_sendCommand != value) { _sendCommand = value; OnPropertyChanged(); } } }
        private string _sendCommand = "PIC1";

        [DisplayName("返回值")]
        public string ExpectedResponse { get => _expectedResponse; set { if (_expectedResponse != value) { _expectedResponse = value; OnPropertyChanged(); } } }
        private string _expectedResponse = "succeed";

        [DisplayName("超时(ms)")]
        public int TimeoutMs { get => _timeoutMs; set { if (_timeoutMs != value) { _timeoutMs = Math.Max(1, value); OnPropertyChanged(); } } }
        private int _timeoutMs = 1000;

        [DisplayName("成功后延时(ms)")]
        public int SuccessDelayMs { get => _successDelayMs; set { if (_successDelayMs != value) { _successDelayMs = Math.Max(0, value); OnPropertyChanged(); } } }
        private int _successDelayMs = 500;

        public PictureSwitchConfig Clone()
        {
            return new PictureSwitchConfig
            {
                IsEnabled = IsEnabled,
                Mode = Mode,
                SendCommand = SendCommand,
                ExpectedResponse = ExpectedResponse,
                TimeoutMs = TimeoutMs,
                SuccessDelayMs = SuccessDelayMs
            };
        }
    }
}