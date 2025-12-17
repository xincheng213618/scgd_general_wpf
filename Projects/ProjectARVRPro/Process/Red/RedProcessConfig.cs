using System.ComponentModel;

namespace ProjectARVRPro.Process.Red
{
    public class RedProcessConfig : ProcessConfigBase
    {
        [Category("解析配置")]
        [DisplayName("Center解析Key")]
        [Description("用于解析Center数据的Key")]
        public string Key_Center { get => _Key_Center; set { _Key_Center = value; OnPropertyChanged(); } }
        private string _Key_Center = "P_5";
    }
}
