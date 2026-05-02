using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;
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
