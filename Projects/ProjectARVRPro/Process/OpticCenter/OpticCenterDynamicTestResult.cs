using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterDynamicViewTestResult : OpticCenterDynamicTestResult
    {
        public OpticCenterTestResult OpticCenterTestResult { get; set; } = new OpticCenterTestResult();
    }

    public class OpticCenterDynamicTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
    }
}
