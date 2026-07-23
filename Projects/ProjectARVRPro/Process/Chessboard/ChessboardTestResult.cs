using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardViewTestResult: ChessboardTestResult
    {
        public List<PoiResultCIExyuvData> PoixyuvDatas { get; set; } = new List<PoiResultCIExyuvData>();
    }

    public class ChessboardTestResult : ViewModelBase
    {
        /// <summary>
        /// 棋盘格对比度 测试项
        /// </summary>
        public ObjectiveTestItem? ChessboardContrast { get; set; }

        /// <summary>
        /// 本地修正后的暗区亮度均值；数据库结果模式下为空。
        /// </summary>
        public ObjectiveTestItem? AverageBlackLuminance { get; set; }

    }
}
