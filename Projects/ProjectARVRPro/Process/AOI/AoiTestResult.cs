using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.AOI
{
    public class AoiViewTestResult : AoiTestResult
    {
    }

    public class AoiTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
    }
}
