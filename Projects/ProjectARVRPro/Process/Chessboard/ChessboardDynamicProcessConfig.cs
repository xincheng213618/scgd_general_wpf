using System.ComponentModel;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardDynamicProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("显示格式")]
        [Description("棋盘格结果显示格式")]
        public string ShowConfig { get => _ShowConfig; set { _ShowConfig = value; OnPropertyChanged(); } }
        private string _ShowConfig = "F3";

        [Category("导出配置")]
        [DisplayName("导出名称")]
        [Description("导出CSV和DynamicTestResults时显示的测试画面名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = "Chessboard";

        [Category("导出配置")]
        [DisplayName("单位")]
        [Description("棋盘格结果单位")]
        public string Unit { get => _Unit; set { _Unit = value; OnPropertyChanged(); } }
        private string _Unit = "%";
    }
}
