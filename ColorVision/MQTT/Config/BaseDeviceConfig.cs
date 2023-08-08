using ColorVision.MVVM;
using System.ComponentModel;

namespace ColorVision.MQTT.Config
{

    public enum CameraType
    {
        [Description("CV_Q")]
        CVQ,
        [Description("LV_Q")]
        LVQ,
        [Description("BV_Q")]
        BVQ,
        [Description("MIL_CL")]
        MILCL,
        [Description("MIL_CXP")]
        MILCXP,
        [Description("BV_H")]
        BVH,
        [Description("LV_H")]
        LVH,
        [Description("HK_CXP")]
        HKCXP,
        [Description("LV_MIL_CL")]
        LVMILCL,
        [Description("MIL_CXP_VIDEO")]
        MILCXPVIDEO,
        [Description("CameraType_Total")]
        CameraTypeTotal,
    };

    public enum TakeImageMode
    {
        [Description("Measure_Normal")]
        Normal = 0,
        [Description("Live")]
        Live,
        [Description("Measure_Fast")]
        Fast,
        [Description("Measure_FastEx")]
        FastExt
    };

    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class BaseDeviceConfig : ViewModelBase
    {
        /// <summary>
        /// 序号
        /// </summary>
        public int No { get => _No; set { _No = value; NotifyPropertyChanged(); } }
        private int _No;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        /// <summary>
        /// 是否存活
        /// </summary>
        public bool IsAlive { get => _IsAlive; set { _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;


        /// <summary>
        /// 设备序号
        /// </summary>
        public string ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private string _ID;

        public string MD5 { get => _MD5; set { _MD5 = value; NotifyPropertyChanged(); } }
        private string _MD5;

        public bool IsRegister { get => _IsRegister; set { _IsRegister = value; NotifyPropertyChanged(); } }
        private bool _IsRegister;

    }
}
