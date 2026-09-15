using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.KeyedResults.FieldOfView
{
    /// <summary>
    /// 按配置名称输出的视场角测试结果。
    /// </summary>
    public class FieldOfViewTestResult : ViewModelBase
    {
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem
        {
            Name = "Horizontal_Field_Of_View_Angle",
            Unit = "degree"
        };

        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem
        {
            Name = "Vertical_Field of_View_Angle",
            Unit = "degree"
        };

        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem
        {
            Name = "Diagonal_Field_of_View_Angle",
            Unit = "degree"
        };
    }
}
