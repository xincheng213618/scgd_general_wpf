using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.MTF.MTF07.MTFH
{
    /// <summary>
    /// 水平方向中心与 0.7F 四角位置的 MTF 测试结果。
    /// </summary>
    public class MTFH07TestResult : ViewModelBase
    {
        public ObjectiveTestItem MTF_H_Center_0F { get; set; } = Item("MTF_H_Center_0F");
        public ObjectiveTestItem MTF_H_LeftUp_0_7F { get; set; } = Item("MTF_H_LeftUp_0_7F");
        public ObjectiveTestItem MTF_H_RightUp_0_7F { get; set; } = Item("MTF_H_RightUp_0_7F");
        public ObjectiveTestItem MTF_H_LeftDown_0_7F { get; set; } = Item("MTF_H_LeftDown_0_7F");
        public ObjectiveTestItem MTF_H_RightDown_0_7F { get; set; } = Item("MTF_H_RightDown_0_7F");

        private static ObjectiveTestItem Item(string name)
        {
            return new ObjectiveTestItem { Name = name, Unit = "%" };
        }
    }
}
