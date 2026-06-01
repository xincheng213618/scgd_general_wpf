using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.MTF.MTFHV
{
    /// <summary>
    /// MTF H/V 清晰度测试结果。字段名中的 H/V 表示水平/垂直方向，Center/LeftUp/RightUp/RightDown/LeftDown 表示视场位置，0F/0.3F/0.6F/0.8F 表示不同视场半径位置。
    /// </summary>
    public class MTFHVTestResult : ViewModelBase
    {
        public ObjectiveTestItem MTF_HV_H_Center_0F { get; set; } = Item("MTF_HV_H_Center_0F");
        public ObjectiveTestItem MTF_HV_V_Center_0F { get; set; } = Item("MTF_HV_V_Center_0F");
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_3F { get; set; } = Item("MTF_HV_H_LeftUp_0_3F");
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_3F { get; set; } = Item("MTF_HV_V_LeftUp_0_3F");
        public ObjectiveTestItem MTF_HV_H_RightUp_0_3F { get; set; } = Item("MTF_HV_H_RightUp_0_3F");
        public ObjectiveTestItem MTF_HV_V_RightUp_0_3F { get; set; } = Item("MTF_HV_V_RightUp_0_3F");
        public ObjectiveTestItem MTF_HV_H_RightDown_0_3F { get; set; } = Item("MTF_HV_H_RightDown_0_3F");
        public ObjectiveTestItem MTF_HV_V_RightDown_0_3F { get; set; } = Item("MTF_HV_V_RightDown_0_3F");
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_3F { get; set; } = Item("MTF_HV_H_LeftDown_0_3F");
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_3F { get; set; } = Item("MTF_HV_V_LeftDown_0_3F");
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_6F { get; set; } = Item("MTF_HV_H_LeftUp_0_6F");
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_6F { get; set; } = Item("MTF_HV_V_LeftUp_0_6F");
        public ObjectiveTestItem MTF_HV_H_RightUp_0_6F { get; set; } = Item("MTF_HV_H_RightUp_0_6F");
        public ObjectiveTestItem MTF_HV_V_RightUp_0_6F { get; set; } = Item("MTF_HV_V_RightUp_0_6F");
        public ObjectiveTestItem MTF_HV_H_RightDown_0_6F { get; set; } = Item("MTF_HV_H_RightDown_0_6F");
        public ObjectiveTestItem MTF_HV_V_RightDown_0_6F { get; set; } = Item("MTF_HV_V_RightDown_0_6F");
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_6F { get; set; } = Item("MTF_HV_H_LeftDown_0_6F");
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_6F { get; set; } = Item("MTF_HV_V_LeftDown_0_6F");
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_8F { get; set; } = Item("MTF_HV_H_LeftUp_0_8F");
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_8F { get; set; } = Item("MTF_HV_V_LeftUp_0_8F");
        public ObjectiveTestItem MTF_HV_H_RightUp_0_8F { get; set; } = Item("MTF_HV_H_RightUp_0_8F");
        public ObjectiveTestItem MTF_HV_V_RightUp_0_8F { get; set; } = Item("MTF_HV_V_RightUp_0_8F");
        public ObjectiveTestItem MTF_HV_H_RightDown_0_8F { get; set; } = Item("MTF_HV_H_RightDown_0_8F");
        public ObjectiveTestItem MTF_HV_V_RightDown_0_8F { get; set; } = Item("MTF_HV_V_RightDown_0_8F");
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_8F { get; set; } = Item("MTF_HV_H_LeftDown_0_8F");
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_8F { get; set; } = Item("MTF_HV_V_LeftDown_0_8F");

        private static ObjectiveTestItem Item(string name)
        {
            return new ObjectiveTestItem { Name = name, Unit = "%" };
        }
    }
}
