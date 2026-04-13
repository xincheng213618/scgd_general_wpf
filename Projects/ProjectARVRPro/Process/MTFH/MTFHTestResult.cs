using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;

namespace ProjectARVRPro.Process.MTFH
{
    /// <summary>
    /// 旧版MTFH测试结果 - 9个测试点（Center_0F + 4角×0.5F + 4角×0.8F）
    /// </summary>
    public class MTFHViewTestResult : MTFHTestResult
    {
        public MTFDetailViewReslut MTFDetailViewReslut { get; set; }
    }

    public class MTFHTestResult : ViewModelBase
    {
        public ObjectiveTestItem MTF_H_Center_0F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_Center_0F", Unit = "%" };

        public ObjectiveTestItem MTF_H_LeftUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_LeftUp_0_5F", Unit = "%" };
        public ObjectiveTestItem MTF_H_RightUp_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_RightUp_0_5F", Unit = "%" };
        public ObjectiveTestItem MTF_H_LeftDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_LeftDown_0_5F", Unit = "%" };
        public ObjectiveTestItem MTF_H_RightDown_0_5F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_RightDown_0_5F", Unit = "%" };

        public ObjectiveTestItem MTF_H_LeftUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_LeftUp_0_8F", Unit = "%" };
        public ObjectiveTestItem MTF_H_RightUp_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_RightUp_0_8F", Unit = "%" };
        public ObjectiveTestItem MTF_H_LeftDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_LeftDown_0_8F", Unit = "%" };
        public ObjectiveTestItem MTF_H_RightDown_0_8F { get; set; } = new ObjectiveTestItem() { Name = "MTF_H_RightDown_0_8F", Unit = "%" };
    }
}
