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

        public ObjectiveTestItem Region_A_Min { get; set; } = new ObjectiveTestItem { Name = "Region_A_Min" };
        public ObjectiveTestItem Region_A_Max { get; set; } = new ObjectiveTestItem { Name = "Region_A_Max" };
        public ObjectiveTestItem Region_A_Average { get; set; } = new ObjectiveTestItem { Name = "Region_A_Average" };

        public ObjectiveTestItem Region_B_Min { get; set; } = new ObjectiveTestItem { Name = "Region_B_Min" };

        public ObjectiveTestItem Region_B_MAx { get; set; } = new ObjectiveTestItem { Name = "Region_B_Max" };

        public ObjectiveTestItem Region_B_Average { get; set; } = new ObjectiveTestItem { Name = "Region_B_Average" };

        public ObjectiveTestItem Region_C_Min { get; set; } = new ObjectiveTestItem { Name = "Region_C_Min" };

        public ObjectiveTestItem Region_C_Max { get; set; } = new ObjectiveTestItem { Name = "Region_C_Max" };

        public ObjectiveTestItem Region_C_Average { get; set; } = new ObjectiveTestItem { Name = "Region_C_Average" };

        public ObjectiveTestItem Region_D_Min { get; set; } = new ObjectiveTestItem { Name = "Region_D_Min" };

        public ObjectiveTestItem Region_D_Max { get; set; } = new ObjectiveTestItem { Name = "Region_D_Max" };

        public ObjectiveTestItem Region_D_Average { get; set; } = new ObjectiveTestItem { Name = "Region_D_Average" };
    }
}
