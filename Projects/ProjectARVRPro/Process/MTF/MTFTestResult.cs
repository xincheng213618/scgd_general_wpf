using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.MTF
{
    public class MTFViewTestResult : MTFTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }
    }

    public class MTFTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new ObservableCollection<ObjectiveTestItem>();
    }
}
