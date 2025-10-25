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
        /// ��ֱTV����(%) ������
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; } = new ObjectiveTestItem() { Name = "Disrotion_KeyStoneVert", Unit = "%" };


        /// <summary>
        /// ˮƽTV����(%) ������
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; } = new ObjectiveTestItem() { Name = "Disrotion_KeyStoneHoriz", Unit = "%" };

        public ObjectiveTestItem DisrotionLeft { get; set; } = new ObjectiveTestItem() { Name = "Disrotion_Left", Unit = "%" };
        public ObjectiveTestItem DisrotionRight { get; set; } = new ObjectiveTestItem() { Name = "Disrotion_Right", Unit = "%" };
        public ObjectiveTestItem DisrotionTop { get; set; } = new ObjectiveTestItem() { Name = "Disrotion_Top", Unit = "%" };
        public ObjectiveTestItem DisrotionDown { get; set; } = new ObjectiveTestItem() { Name = "Disrotion_Down", Unit = "%" };

    }
}
