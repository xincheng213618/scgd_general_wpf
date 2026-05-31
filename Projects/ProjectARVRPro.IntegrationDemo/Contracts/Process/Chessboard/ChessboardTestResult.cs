using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.Chessboard
{
    /// <summary>
    /// 棋盘格测试结果。
    /// </summary>
    public class ChessboardTestResult : ViewModelBase
    {
        /// <summary>棋盘格对比度，基于棋盘格亮暗区域计算的对比度指标。</summary>
        public ObjectiveTestItem ChessboardContrast { get; set; }
    }
}
