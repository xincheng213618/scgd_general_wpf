using System.ComponentModel;

namespace ProjectARVRPro.Process.MTFV
{
    /// <summary>
    /// 旧版MTFV解析配置 - 使用 MTFResult.result + mtfValue（旧版解析）
    /// </summary>
    public class MTFVProcessConfig : ProcessConfigBase
    {
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F1";

        [Category("解析配置(旧版)")]
        [DisplayName("Center_0F_V解析Key")]
        public string Key_Center_0F { get => _Key_Center_0F; set { _Key_Center_0F = value; OnPropertyChanged(); } }
        private string _Key_Center_0F = "Center_0F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("LeftUp_0.5F_V解析Key")]
        public string Key_LeftUp_0_5F { get => _Key_LeftUp_0_5F; set { _Key_LeftUp_0_5F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_5F = "LeftUp_0.5F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("RightUp_0.5F_V解析Key")]
        public string Key_RightUp_0_5F { get => _Key_RightUp_0_5F; set { _Key_RightUp_0_5F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_5F = "RightUp_0.5F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("LeftDown_0.5F_V解析Key")]
        public string Key_LeftDown_0_5F { get => _Key_LeftDown_0_5F; set { _Key_LeftDown_0_5F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_5F = "LeftDown_0.5F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("RightDown_0.5F_V解析Key")]
        public string Key_RightDown_0_5F { get => _Key_RightDown_0_5F; set { _Key_RightDown_0_5F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_5F = "RightDown_0.5F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("LeftUp_0.8F_V解析Key")]
        public string Key_LeftUp_0_8F { get => _Key_LeftUp_0_8F; set { _Key_LeftUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftUp_0_8F = "LeftUp_0.8F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("RightUp_0.8F_V解析Key")]
        public string Key_RightUp_0_8F { get => _Key_RightUp_0_8F; set { _Key_RightUp_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightUp_0_8F = "RightUp_0.8F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("LeftDown_0.8F_V解析Key")]
        public string Key_LeftDown_0_8F { get => _Key_LeftDown_0_8F; set { _Key_LeftDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_LeftDown_0_8F = "LeftDown_0.8F_V";

        [Category("解析配置(旧版)")]
        [DisplayName("RightDown_0.8F_V解析Key")]
        public string Key_RightDown_0_8F { get => _Key_RightDown_0_8F; set { _Key_RightDown_0_8F = value; OnPropertyChanged(); } }
        private string _Key_RightDown_0_8F = "RightDown_0.8F_V";
    }
}
