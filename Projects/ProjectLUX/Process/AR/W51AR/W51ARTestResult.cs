using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ProjectLUX.Process.AR.W51AR
{
    public class W51ARViewTestResult : W51ARTestResult
    {
    }

    public class W51ARTestResult : ViewModelBase
    {
        /// <summary>
        /// 水平视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Horizontal_Field_Of_View_Angle" , Unit = "degree" };

        /// <summary>
        /// 垂直视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Vertical_Field of_View_Angle", Unit = "degree" };
        /// <summary>
        /// 对角线视场角(°) 测试项
        /// </summary>
        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new ObjectiveTestItem() { Name = "Diagonal_Field_of_View_Angle", Unit = "degree" };
    }
}
