using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionTestResult : ViewModelBase
    {
        /// <summary>
        /// ˮƽTV����(%) ������
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; }

        /// <summary>
        /// ��ֱTV����(%) ������
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; }

        public List<Point> Points { get; set; } = new List<Point>();
    }
}
