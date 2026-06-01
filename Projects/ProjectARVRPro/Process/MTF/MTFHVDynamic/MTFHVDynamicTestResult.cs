using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.MTF.MTFHVDynamic
{
    public class MTFHVDynamicViewTestResult : MTFHVDynamicTestResult
    {
        public MTFDetailViewReslut? MTFDetailViewReslut { get; set; }
    }

    public class MTFHVDynamicTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
    }
}
