using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectLUX.Process.DistortionAR
{
    public class DistortionARViewTestResult: DistortionARTestResult
    {
        public List<Point> Points { get; set; } = new List<Point>();
    }

    public class DistortionARTestResult : ViewModelBase
    {
        /// <summary>
        /// ˮƽTV����(%) ������
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; } = new ObjectiveTestItem() { Name = "SMIA_TV_Distortion_Horizontal", Unit = "%" };

        /// <summary>
        /// ��ֱTV����(%) ������
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; } = new ObjectiveTestItem() { Name = "SMIA_TV_Distortion_Vertical", Unit = "%" };


    }
}
