using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFHV048
{
    public class MTFHV048ProcessConfig : ProcessConfigBase
    {
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F1";

        [Category("解析配置")]
        [DisplayName("Center_0F解析Key")]
        [Description("用于解析Center_0F数据的Key")]
        public string Key_Center_0F { get => _Key_Center_0F; set { _Key_Center_0F = value; OnPropertyChanged(); } }
        private string _Key_Center_0F = "Center_0F";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_4F解析Key")]
        [Description("用于解析LeftUp_0_4F数据的Key")]
        public string Key_LeftUp_0_4F { get => _Key_LeftUp_0_4F; set { _Key_LeftUp_0_4F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_4F = "LeftUp_0.4F";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_4F解析Key")]
        [Description("用于解析LeftDown_0_4F数据的Key")]
        public string Key_LeftDown_0_4F { get => _Key_LeftDown_0_4F; set { _Key_LeftDown_0_4F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_4F = "LeftDown_0.4F";

        [Category("解析配置")]
        [DisplayName("RightDown_0_4F解析Key")]
        [Description("用于解析RightDown_0_4F数据的Key")]
        public string Key_RightDown_0_4F { get => _Key_RightDown_0_4F; set { _Key_RightDown_0_4F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_4F = "RightDown_0.4F";

        [Category("解析配置")]
        [DisplayName("RightUp_0_4F解析Key")]
        [Description("用于解析RightUp_0_4F数据的Key")]
        public string Key_RightUp_0_4F { get => _Key_RightUp_0_4F; set { _Key_RightUp_0_4F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_4F = "RightUp_0.4F";

        [Category("解析配置")]
        [DisplayName("LeftUp_0_8F解析Key")]
        [Description("用于解析LeftUp_0_8F数据的Key")]
        public string Key_LeftUp_0_8F { get => _Key_LeftUp_0_8F; set { _Key_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_8F = "LeftUp_0.8F";

        [Category("解析配置")]
        [DisplayName("LeftDown_0_8F解析Key")]
        [Description("用于解析LeftDown_0_8F数据的Key")]
        public string Key_LeftDown_0_8F { get => _Key_LeftDown_0_8F; set { _Key_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_8F = "LeftDown_0.8F";

        [Category("解析配置")]
        [DisplayName("RightDown_0_8F解析Key")]
        [Description("用于解析RightDown_0_8F数据的Key")]
        public string Key_RightDown_0_8F { get => _Key_RightDown_0_8F; set { _Key_RightDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_8F = "RightDown_0.8F";

        [Category("解析配置")]
        [DisplayName("RightUp_0_8F解析Key")]
        [Description("用于解析RightUp_0_8F数据的Key")]
        public string Key_RightUp_0_8F { get => _Key_RightUp_0_8F; set { _Key_RightUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_8F = "RightUp_0.8F";
    }
}
