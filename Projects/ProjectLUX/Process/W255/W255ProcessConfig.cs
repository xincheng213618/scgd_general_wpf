using System.ComponentModel;

namespace ProjectLUX.Process.W255
{
    public class W255ProcessConfig : ProcessConfigBase
    {
        [Category("解析配置")]
        [DisplayName("Center解析Key")]
        [Description("用于解析Center数据的Key")]
        public string Key_Center { get => _Key_Center; set { _Key_Center = value; OnPropertyChanged(); } }
        private string _Key_Center = "P_5";

        [Category("解析配置")]
        [DisplayName("LuminanceUniformityTempName")]
        [Description("Luminance_uniformity")]
        public string LuminanceUniformityTempName { get => _LuminanceUniformityTempName; set { _LuminanceUniformityTempName = value; OnPropertyChanged(); } }
        private string _LuminanceUniformityTempName = "Luminance_uniformity";


        [Category("解析配置")]
        [DisplayName("ColorUniformityTempName")]
        [Description("Color_uniformity")]
        public string ColorUniformityTempName { get => _ColorUniformityTempName; set { _ColorUniformityTempName = value; OnPropertyChanged(); } }
        private string _ColorUniformityTempName = "Color_uniformity";

    }
}
