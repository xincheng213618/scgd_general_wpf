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
        /// <summary>
        /// ���̸�Աȶ� ������
        /// </summary>
        public ObjectiveTestItem ChessboardContrast { get; set; } = new ObjectiveTestItem();

    }
}
