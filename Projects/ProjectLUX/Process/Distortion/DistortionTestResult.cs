using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectLUX.Process.Distortion
{
    public class DistortionViewTestResult: DistortionTestResult
    {
        public List<Point> Points { get; set; } = new List<Point>();
    }

    public class DistortionTestResult : ViewModelBase
    {
        /// <summary>
        /// 水平TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// 垂直TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; } = new ObjectiveTestItem();

    }
}
