using System.ComponentModel;
using Newtonsoft.Json;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardDynamicProcessConfig : ProcessConfigBase
    {
        [Category("棋盘格对比度")]
        [DisplayName("数据库结果名称")]
        [Description("存在名称包含该值的棋盘格对比度数据库结果时沿用原结果；不存在时使用POI本地计算。")]
        public string ChessboardContrastResultName { get => _ChessboardContrastResultName; set { _ChessboardContrastResultName = value; OnPropertyChanged(); } }
        private string _ChessboardContrastResultName = "Chessboard_Contrast";

        [Category("棋盘格对比度")]
        [DisplayName("棋盘格行数")]
        [Description("行数和列数都为0时按POI数量自动推断方阵；矩形棋盘格需同时设置行数和列数。")]
        public int RowCount { get => _RowCount; set { _RowCount = value; OnPropertyChanged(); } }
        private int _RowCount;

        [Category("棋盘格对比度")]
        [DisplayName("棋盘格列数")]
        [Description("行数和列数都为0时按POI数量自动推断方阵；矩形棋盘格需同时设置行数和列数。")]
        public int ColumnCount { get => _ColumnCount; set { _ColumnCount = value; OnPropertyChanged(); } }
        private int _ColumnCount;

        [Category("棋盘格对比度")]
        [DisplayName("左上角颜色")]
        [Description("Auto按两组交替格的平均亮度自动判断黑白；也可手动指定左上角为Black或White。")]
        public ChessboardFirstPointColor FirstPointColor { get => _FirstPointColor; set { _FirstPointColor = value; OnPropertyChanged(); } }
        private ChessboardFirstPointColor _FirstPointColor = ChessboardFirstPointColor.Auto;

        [Browsable(false)]
        [JsonProperty("FirstPointIsBlack")]
        private bool LegacyFirstPointIsBlack { set => FirstPointColor = value ? ChessboardFirstPointColor.Black : ChessboardFirstPointColor.White; }

        [Category("棋盘格对比度")]
        [DisplayName("杂散光系数")]
        [Description("本地计算时先求暗格平均亮度LD，再使用LD'=LD-LB*a修正均值；单格POI保持原值，默认0表示不补偿。")]
        public double StrayLightCoefficient { get => _StrayLightCoefficient; set { _StrayLightCoefficient = value; OnPropertyChanged(); } }
        private double _StrayLightCoefficient;

        [Category("棋盘格对比度")]
        [DisplayName("允许显示负值修正结果")]
        [Description("启用后，修正后的暗格平均亮度为负数时仍输出暗格均值和棋盘格对比度；等于0时仍无法计算。")]
        public bool AllowNegativeCorrectedDarkLuminance { get => _AllowNegativeCorrectedDarkLuminance; set { _AllowNegativeCorrectedDarkLuminance = value; OnPropertyChanged(); } }
        private bool _AllowNegativeCorrectedDarkLuminance;

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
