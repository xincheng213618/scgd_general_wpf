using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionDynamicViewTestResult : DistortionDynamicTestResult
    {
        public DistortionViewTestResult DistortionViewTestResult { get; set; } = new DistortionViewTestResult();
    }

    public class DistortionDynamicTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
    }
}
