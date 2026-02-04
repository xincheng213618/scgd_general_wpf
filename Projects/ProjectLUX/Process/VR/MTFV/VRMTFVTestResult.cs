using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectLUX.Process.VR.MTFV
{
    public class VRMTFVViewTestResult : VRMTFVTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class VRMTFVTestResult : ViewModelBase
    {
        public List<ObjectiveTestItem> ObjectiveTestItems { get; set; } = new List<ObjectiveTestItem>();
    }
}
