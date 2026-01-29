using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFHV
{
    public class MTFHVProcessConfig : ProcessConfigBase
    {
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F1";

        [Category("解析配置")]
        [DisplayName("Center_0F解析Key")]
        [Description("用于解析Center_0F数据的Key")]
        public string Key_Center_0F { get => _Key_Center_0F; set { _Key_Center_0F = value; OnPropertyChanged(); } }
        private string _Key_Center_0F = "0F_MTF_HV_Center";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_3F解析Key")]
        [Description("用于解析LeftUp_0_3F数据的Key")]
        public string Key_LeftUp_0_3F { get => _Key_LeftUp_0_3F; set { _Key_LeftUp_0_3F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_3F = "0.3F_MTF_HV_LeftUp";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_3F解析Key")]
        [Description("用于解析LeftDown_0_3F数据的Key")]
        public string Key_LeftDown_0_3F { get => _Key_LeftDown_0_3F; set { _Key_LeftDown_0_3F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_3F = "0.3F_MTF_HV_LeftDown";

        [Category("解析配置")]
        [DisplayName("RightDown_0_3F解析Key")]
        [Description("用于解析RightDown_0_3F数据的Key")]
        public string Key_RightDown_0_3F { get => _Key_RightDown_0_3F; set { _Key_RightDown_0_3F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_3F = "0.3F_MTF_HV_RightDown";

        [Category("解析配置")]
        [DisplayName("RightUp_0_3F解析Key")]
        [Description("用于解析RightUp_0_3F数据的Key")]
        public string Key_RightUp_0_3F { get => _Key_RightUp_0_3F; set { _Key_RightUp_0_3F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_3F = "0.3F_MTF_HV_RightUp";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_6F解析Key")]
        [Description("用于解析LeftUp_0_6F数据的Key")]
        public string Key_LeftUp_0_6F { get => _Key_LeftUp_0_6F; set { _Key_LeftUp_0_6F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_6F = "0.6F_MTF_HV_LeftUp";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_6F解析Key")]
        [Description("用于解析LeftDown_0_6F数据的Key")]
        public string Key_LeftDown_0_6F { get => _Key_LeftDown_0_6F; set { _Key_LeftDown_0_6F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_6F = "0.6F_MTF_HV_LeftDown";

        [Category("解析配置")]
        [DisplayName("RightDown_0_6F解析Key")]
        [Description("用于解析RightDown_0_6F数据的Key")]
        public string Key_RightDown_0_6F { get => _Key_RightDown_0_6F; set { _Key_RightDown_0_6F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_6F = "0.6F_MTF_HV_RightDown";

        [Category("解析配置")]
        [DisplayName("RightUp_0_6F解析Key")]
        [Description("用于解析RightUp_0_6F数据的Key")]
        public string Key_RightUp_0_6F { get => _Key_RightUp_0_6F; set { _Key_RightUp_0_6F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_6F = "0.6F_MTF_HV_RightUp";

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
