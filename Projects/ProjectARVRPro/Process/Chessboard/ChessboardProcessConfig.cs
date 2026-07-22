using System.ComponentModel;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardProcessConfig : ProcessConfigBase
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
        [DisplayName("左上角为黑格")]
        [Description("按POI空间位置排序后，将左上角视为首点，并据此交替识别黑白格。")]
        public bool FirstPointIsBlack { get => _FirstPointIsBlack; set { _FirstPointIsBlack = value; OnPropertyChanged(); } }
        private bool _FirstPointIsBlack = true;

        [Category("棋盘格对比度")]
        [DisplayName("杂散光系数")]
        [Description("本地计算时先求暗格平均亮度LD，再使用LD'=LD-LB*a修正均值；单格POI保持原值，默认0表示不补偿。")]
        public double StrayLightCoefficient { get => _StrayLightCoefficient; set { _StrayLightCoefficient = value; OnPropertyChanged(); } }
        private double _StrayLightCoefficient;
    }
}
