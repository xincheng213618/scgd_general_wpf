using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.FindLightArea;

namespace ProjectARVRPro.Process.KeyedResults.FieldOfView
{
    public class FieldOfViewViewTestResult : FieldOfViewTestResult
    {
        public List<AlgResultLightAreaModel> AlgResultLightAreaModels { get; set; } = new();
    }

    public class FieldOfViewTestResult : ViewModelBase
    {
        public ObjectiveTestItem HorizontalFieldOfViewAngle { get; set; } = new()
        {
            Name = "Horizontal_Field_Of_View_Angle",
            Unit = "degree"
        };

        public ObjectiveTestItem VerticalFieldOfViewAngle { get; set; } = new()
        {
            Name = "Vertical_Field of_View_Angle",
            Unit = "degree"
        };

        public ObjectiveTestItem DiagonalFieldOfViewAngle { get; set; } = new()
        {
            Name = "Diagonal_Field_of_View_Angle",
            Unit = "degree"
        };
    }
}
