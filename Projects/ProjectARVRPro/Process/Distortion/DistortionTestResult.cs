using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionTestResult : ViewModelBase
    {
        /// <summary>
        /// 水平TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; }

        /// <summary>
        /// 垂直TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; }

        public List<Point> Points { get; set; } = new List<Point>();
    }
}
