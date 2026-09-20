using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.MTF.MTF07.MTFV
{
    /// <summary>
    /// 垂直方向中心与 0.7F 四角位置的 MTF 测试结果。
    /// </summary>
    public class MTFV07TestResult : ViewModelBase
    {
        public ObjectiveTestItem MTF_V_Center_0F { get; set; } = Item("MTF_V_Center_0F");
        public ObjectiveTestItem MTF_V_LeftUp_0_7F { get; set; } = Item("MTF_V_LeftUp_0_7F");
        public ObjectiveTestItem MTF_V_RightUp_0_7F { get; set; } = Item("MTF_V_RightUp_0_7F");
        public ObjectiveTestItem MTF_V_LeftDown_0_7F { get; set; } = Item("MTF_V_LeftDown_0_7F");
        public ObjectiveTestItem MTF_V_RightDown_0_7F { get; set; } = Item("MTF_V_RightDown_0_7F");

        private static ObjectiveTestItem Item(string name)
        {
            return new ObjectiveTestItem { Name = name, Unit = "%" };
        }
    }
}
