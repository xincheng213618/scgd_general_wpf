using ColorVision.Common.MVVM;
using ColorVision.UI;
using cvColorVision;
using System.ComponentModel;

namespace Spectrum.Configs
{
    public class SmuConfig : ViewModelBase, IConfig
    {
        public static SmuConfig Instance => ConfigService.Instance.GetRequiredService<SmuConfig>();

        [DisplayName("自动连接")]
        public bool IsAutoStart { get => _IsAutoStart; set { _IsAutoStart = value; OnPropertyChanged(); } }
        private bool _IsAutoStart;

        [DisplayName("设备名称")]
        public string DevName { get => _DevName; set { _DevName = value; OnPropertyChanged(); } }
        private string _DevName = "GPIB0::24::INSTR";

        [DisplayName("网络连接")]
        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet = true;

        [DisplayName("源表类型")]
        public Pss_Type DevType { get => _DevType; set { _DevType = value; OnPropertyChanged(); OnPropertyChanged(nameof(PssType)); } }
        private Pss_Type _DevType = Pss_Type.Keithley_2400;

        [Browsable(false)]
        public Pss_Type PssType { get => DevType; set => DevType = value; }

        [DisplayName("延迟时间(ms)")]
        public double DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private double _DelayTime;

        [DisplayName("四线制")]
        public bool Is4Wire { get => _Is4Wire; set { _Is4Wire = value; OnPropertyChanged(); } }
        private bool _Is4Wire;

        [DisplayName("前接口")]
        public bool IsFront { get => _IsFront; set { _IsFront = value; OnPropertyChanged(); } }
        private bool _IsFront = true;
    }
}
