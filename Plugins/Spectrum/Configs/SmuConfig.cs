using ColorVision.Common.MVVM;
using ColorVision.UI;
using cvColorVision;
using System.ComponentModel;

namespace Spectrum.Configs
{
    public class SmuConfig : ViewModelBase, IConfig
    {
        public static SmuConfig Instance => ConfigService.Instance.GetRequiredService<SmuConfig>();

        [DisplayName("设备名称")]
        public string DevName { get => _DevName; set { _DevName = value; OnPropertyChanged(); } }
        private string _DevName = "GPIB0::24::INSTR";

        [DisplayName("网络连接")]
        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet = true;

        [DisplayName("源表类型")]
        public Pss_Type PssType { get => _PssType; set { _PssType = value; OnPropertyChanged(); } }
        private Pss_Type _PssType = Pss_Type.kethiley2400;

        [DisplayName("电压源模式")]
        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; OnPropertyChanged(); } }
        private bool _IsSourceV = true;

        [DisplayName("通道A")]
        public bool IsChannelA { get => _IsChannelA; set { _IsChannelA = value; OnPropertyChanged(); } }
        private bool _IsChannelA = true;

        [DisplayName("四线制")]
        public bool Is4Wire { get => _Is4Wire; set { _Is4Wire = value; OnPropertyChanged(); } }
        private bool _Is4Wire = false;

        [DisplayName("前接口")]
        public bool IsFront { get => _IsFront; set { _IsFront = value; OnPropertyChanged(); } }
        private bool _IsFront = true;

        [DisplayName("测量值 (V/mA)")]
        public double MeasureVal { get => _MeasureVal; set { _MeasureVal = value; OnPropertyChanged(); } }
        private double _MeasureVal = 5.0;

        [DisplayName("限制值 (mA/V)")]
        public double LimitVal { get => _LimitVal; set { _LimitVal = value; OnPropertyChanged(); } }
        private double _LimitVal = 100.0;
    }
}
