using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectLUX.Process.VR.MTFH
{
    public class VRMTFHViewTestResult : VRMTFHTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }

    }

    public class VRMTFHTestResult : ViewModelBase
    {
        public List<ObjectiveTestItem> ObjectiveTestItems { get; set; } = new List<ObjectiveTestItem>();

    }
}
