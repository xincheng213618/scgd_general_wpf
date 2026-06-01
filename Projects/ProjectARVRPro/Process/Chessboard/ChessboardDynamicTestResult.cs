using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardDynamicViewTestResult : ChessboardDynamicTestResult
    {
        public ChessboardViewTestResult ChessboardViewTestResult { get; set; } = new ChessboardViewTestResult();
    }

    public class ChessboardDynamicTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
    }
}
