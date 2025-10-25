using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectLUX.Process.Chessboard
{
    public class ChessboardViewTestResult: ChessboardTestResult
    {
        public List<PoiResultCIExyuvData> PoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }

    public class ChessboardTestResult : ViewModelBase
    {
        public ObjectiveTestItem P1Lv { get; set; } = new ObjectiveTestItem() { Name = "P1(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P2Lv { get; set; } = new ObjectiveTestItem() { Name = "P2(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P3Lv { get; set; } = new ObjectiveTestItem() { Name = "P3(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P4Lv { get; set; } = new ObjectiveTestItem() { Name = "P4(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P5Lv { get; set; } = new ObjectiveTestItem() { Name = "P5(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P6Lv { get; set; } = new ObjectiveTestItem() { Name = "P6(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P7Lv { get; set; } = new ObjectiveTestItem() { Name = "P7(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P8Lv { get; set; } = new ObjectiveTestItem() { Name = "P8(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P9Lv { get; set; } = new ObjectiveTestItem() { Name = "P9(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P10Lv { get; set; } = new ObjectiveTestItem() { Name = "P10(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P11Lv { get; set; } = new ObjectiveTestItem() { Name = "P11(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P12Lv { get; set; } = new ObjectiveTestItem() { Name = "P12(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P13Lv { get; set; } = new ObjectiveTestItem() { Name = "P13(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P14Lv { get; set; } = new ObjectiveTestItem() { Name = "P14(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P15Lv { get; set; } = new ObjectiveTestItem() { Name = "P15(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P16Lv { get; set; } = new ObjectiveTestItem() { Name = "P16(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P17Lv { get; set; } = new ObjectiveTestItem() { Name = "P17(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P18Lv { get; set; } = new ObjectiveTestItem() { Name = "P18(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P19Lv { get; set; } = new ObjectiveTestItem() { Name = "P19(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P20Lv { get; set; } = new ObjectiveTestItem() { Name = "P20(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P21Lv { get; set; } = new ObjectiveTestItem() { Name = "P21(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P22Lv { get; set; } = new ObjectiveTestItem() { Name = "P22(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P23Lv { get; set; } = new ObjectiveTestItem() { Name = "P23(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P24Lv { get; set; } = new ObjectiveTestItem() { Name = "P24(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P25Lv { get; set; } = new ObjectiveTestItem() { Name = "P25(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P26Lv { get; set; } = new ObjectiveTestItem() { Name = "P26(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P27Lv { get; set; } = new ObjectiveTestItem() { Name = "P27(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P28Lv { get; set; } = new ObjectiveTestItem() { Name = "P28(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P29Lv { get; set; } = new ObjectiveTestItem() { Name = "P29(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P30Lv { get; set; } = new ObjectiveTestItem() { Name = "P30(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P31Lv { get; set; } = new ObjectiveTestItem() { Name = "P31(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P32Lv { get; set; } = new ObjectiveTestItem() { Name = "P32(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P33Lv { get; set; } = new ObjectiveTestItem() { Name = "P33(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P34Lv { get; set; } = new ObjectiveTestItem() { Name = "P34(Lv)", Unit = "cd/m^2" };
        public ObjectiveTestItem P35Lv { get; set; } = new ObjectiveTestItem() { Name = "P35(Lv)", Unit = "cd/m^2" };

        public ObjectiveTestItem AverageWhiteLunimance { get; set; } = new ObjectiveTestItem() { Name = "Average_White_Lunimance", Unit = "cd/m^2" };

        public ObjectiveTestItem AverageBlackLunimance { get; set; } = new ObjectiveTestItem() { Name = "Average_Black_Lunimance", Unit = "cd/m^2" };
        /// <summary>
        /// 棋盘格对比度 测试项
        /// </summary>
        public ObjectiveTestItem ChessboardContrast { get; set; } = new ObjectiveTestItem() { Name = "Contrast_radio" };

    }
}
