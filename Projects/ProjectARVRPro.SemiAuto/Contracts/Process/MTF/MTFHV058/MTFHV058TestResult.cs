using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.MTF.MTFHV058
{
    /// <summary>
    /// MTF 0.5F/0.8F H/V 清晰度测试结果。字段名中的 H/V 表示水平/垂直方向，位置名表示视场方位。
    /// </summary>
    public class MTFHV058TestResult : ViewModelBase
    {
        public ObjectiveTestItem MTF_HV_H_Center_0F { get; set; } = Item("MTF_HV_H_Center_0F");
        public ObjectiveTestItem MTF_HV_Center_0F { get; set; } = Item("MTF_HV_Center_0F");
        public ObjectiveTestItem MTF_HV_V_Center_0F { get; set; } = Item("MTF_HV_V_Center_0F");
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_5F { get; set; } = Item("MTF_HV_H_LeftUp_0_5F");
        public ObjectiveTestItem MTF_HV_LeftUp_0_5F { get; set; } = Item("MTF_HV_LeftUp_0_5F");
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_5F { get; set; } = Item("MTF_HV_V_LeftUp_0_5F");
        public ObjectiveTestItem MTF_HV_H_RightUp_0_5F { get; set; } = Item("MTF_HV_H_RightUp_0_5F");
        public ObjectiveTestItem MTF_HV_RightUp_0_5F { get; set; } = Item("MTF_HV_RightUp_0_5F");
        public ObjectiveTestItem MTF_HV_V_RightUp_0_5F { get; set; } = Item("MTF_HV_V_RightUp_0_5F");
        public ObjectiveTestItem MTF_HV_H_RightDown_0_5F { get; set; } = Item("MTF_HV_H_RightDown_0_5F");
        public ObjectiveTestItem MTF_HV_RightDown_0_5F { get; set; } = Item("MTF_HV_RightDown_0_5F");
        public ObjectiveTestItem MTF_HV_V_RightDown_0_5F { get; set; } = Item("MTF_HV_V_RightDown_0_5F");
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_5F { get; set; } = Item("MTF_HV_H_LeftDown_0_5F");
        public ObjectiveTestItem MTF_HV_LeftDown_0_5F { get; set; } = Item("MTF_HV_LeftDown_0_5F");
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_5F { get; set; } = Item("MTF_HV_V_LeftDown_0_5F");
        public ObjectiveTestItem MTF_HV_H_LeftUp_0_8F { get; set; } = Item("MTF_HV_H_LeftUp_0_8F");
        public ObjectiveTestItem MTF_HV_LeftUp_0_8F { get; set; } = Item("MTF_HV_LeftUp_0_8F");
        public ObjectiveTestItem MTF_HV_V_LeftUp_0_8F { get; set; } = Item("MTF_HV_V_LeftUp_0_8F");
        public ObjectiveTestItem MTF_HV_H_RightUp_0_8F { get; set; } = Item("MTF_HV_H_RightUp_0_8F");
        public ObjectiveTestItem MTF_HV_RightUp_0_8F { get; set; } = Item("MTF_HV_RightUp_0_8F");
        public ObjectiveTestItem MTF_HV_V_RightUp_0_8F { get; set; } = Item("MTF_HV_V_RightUp_0_8F");
        public ObjectiveTestItem MTF_HV_H_RightDown_0_8F { get; set; } = Item("MTF_HV_H_RightDown_0_8F");
        public ObjectiveTestItem MTF_HV_RightDown_0_8F { get; set; } = Item("MTF_HV_RightDown_0_8F");
        public ObjectiveTestItem MTF_HV_V_RightDown_0_8F { get; set; } = Item("MTF_HV_V_RightDown_0_8F");
        public ObjectiveTestItem MTF_HV_H_LeftDown_0_8F { get; set; } = Item("MTF_HV_H_LeftDown_0_8F");
        public ObjectiveTestItem MTF_HV_LeftDown_0_8F { get; set; } = Item("MTF_HV_LeftDown_0_8F");
        public ObjectiveTestItem MTF_HV_V_LeftDown_0_8F { get; set; } = Item("MTF_HV_V_LeftDown_0_8F");

        private static ObjectiveTestItem Item(string name)
        {
            return new ObjectiveTestItem { Name = name, Unit = "%" };
        }
    }
}
