using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFHV058
{
    public class MTFHV058ProcessConfig : ProcessConfigBase
    {
        [Category("解析配置")]
        [DisplayName("Center_0F解析Key")]
        [Description("用于解析Center_0F数据的Key")]
        public string Key_Center_0F { get => _Key_Center_0F; set { _Key_Center_0F = value; OnPropertyChanged(); } }
        private string _Key_Center_0F = "0F_MTF_HV_Center";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_5F解析Key")]
        [Description("用于解析LeftUp_0_5F数据的Key")]
        public string Key_LeftUp_0_5F { get => _Key_LeftUp_0_5F; set { _Key_LeftUp_0_5F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_5F = "0.5F_MTF_HV_LeftUp";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_5F解析Key")]
        [Description("用于解析LeftDown_0_5F数据的Key")]
        public string Key_LeftDown_0_5F { get => _Key_LeftDown_0_5F; set { _Key_LeftDown_0_5F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_5F = "0.5F_MTF_HV_LeftDown";

        [Category("解析配置")]
        [DisplayName("RightDown_0_5F解析Key")]
        [Description("用于解析RightDown_0_5F数据的Key")]
        public string Key_RightDown_0_5F { get => _Key_RightDown_0_5F; set { _Key_RightDown_0_5F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_5F = "0.5F_MTF_HV_RightDown";

        [Category("解析配置")]
        [DisplayName("RightUp_0_5F解析Key")]
        [Description("用于解析RightUp_0_5F数据的Key")]
        public string Key_RightUp_0_5F { get => _Key_RightUp_0_5F; set { _Key_RightUp_0_5F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_5F = "0.5F_MTF_HV_RightUp";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_8F解析Key")]
        [Description("用于解析LeftUp_0_8F数据的Key")]
        public string Key_LeftUp_0_8F { get => _Key_LeftUp_0_8F; set { _Key_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_8F = "0.8F_MTF_HV_LeftUp";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_8F解析Key")]
        [Description("用于解析LeftDown_0_8F数据的Key")]
        public string Key_LeftDown_0_8F { get => _Key_LeftDown_0_8F; set { _Key_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_8F = "0.8F_MTF_HV_LeftDown";

        [Category("解析配置")]
        [DisplayName("RightDown_0_8F解析Key")]
        [Description("用于解析RightDown_0_8F数据的Key")]
        public string Key_RightDown_0_8F { get => _Key_RightDown_0_8F; set { _Key_RightDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_8F = "0.8F_MTF_HV_RightDown";

        [Category("解析配置")]
        [DisplayName("RightUp_0_8F解析Key")]
        [Description("用于解析RightUp_0_8F数据的Key")]
        public string Key_RightUp_0_8F { get => _Key_RightUp_0_8F; set { _Key_RightUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_8F = "0.8F_MTF_HV_RightUp";
    }
}
