using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectARVRPro.Process.MTFV
{
    /// <summary>
    /// 旧版MTFV测试结果 - 9个测试点（Center_0F + 4角×0.5F + 4角×0.8F）
    /// </summary>
    public class MTFVViewTestResult : MTFVTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }
    }

    public class MTFVTestResult : ViewModelBase
    {
        public ObjectiveTestItem MTF_V_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_Center_0F", Unit = "%" };

        public ObjectiveTestItem MTF_V_LeftUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_LeftUp_0_5F", Unit = "%" };
        public ObjectiveTestItem MTF_V_RightUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_RightUp_0_5F", Unit = "%" };
        public ObjectiveTestItem MTF_V_LeftDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_LeftDown_0_5F", Unit = "%" };
        public ObjectiveTestItem MTF_V_RightDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_RightDown_0_5F", Unit = "%" };

        public ObjectiveTestItem MTF_V_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_LeftUp_0_8F", Unit = "%" };
        public ObjectiveTestItem MTF_V_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_RightUp_0_8F", Unit = "%" };
        public ObjectiveTestItem MTF_V_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_LeftDown_0_8F", Unit = "%" };
        public ObjectiveTestItem MTF_V_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_V_RightDown_0_8F", Unit = "%" };
    }
}
