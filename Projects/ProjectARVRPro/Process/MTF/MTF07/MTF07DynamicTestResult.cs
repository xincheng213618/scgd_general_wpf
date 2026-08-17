using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;
using System.Collections.ObjectModel;

namespace ProjectARVRPro.Process.MTF.MTF07
{
    public class MTF07DynamicViewTestResult : MTF07DynamicTestResult
    {
        public MTFDetailViewReslut? MTFDetailViewReslut { get; set; }
    }

    public class MTF07DynamicTestResult : ViewModelBase
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new();
    }
}
