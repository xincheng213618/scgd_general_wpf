using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.W51
{
    /// <summary>
    /// W51 视场角测试结果。
    /// </summary>
    public class W51TestResult : ViewModelBase
    {
        /// <summary>水平视场角，表示画面水平方向可观察范围，单位 degree。</summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem { Name = "Horizontal_Field_Of_View_Angle", Unit = "degree" };
        /// <summary>垂直视场角，表示画面垂直方向可观察范围，单位 degree。</summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem { Name = "Vertical_Field of_View_Angle", Unit = "degree" };
        /// <summary>对角线视场角，表示画面对角方向可观察范围，单位 degree。</summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem { Name = "Diagonal_Field_of_View_Angle", Unit = "degree" };
    }
}
