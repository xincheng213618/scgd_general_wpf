using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectLUX.Process.OpticCenter
{
    public class OpticCenterTestResult : ViewModelBase
    {
        /// <summary>
        /// X����б��(��) ������
        /// </summary>
        public ObjectiveTestItem ImageCenterXTilt { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// Y����б��(��) ������
        /// </summary>
        public ObjectiveTestItem ImageCenterYTilt { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// ��ת��(��) ������
        /// </summary>
        public ObjectiveTestItem ImageCenterRotation { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// ��ת��(��) ������
        /// </summary>
        public ObjectiveTestItem OptCenterRotation { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// X����б��(��) ������
        /// </summary>
        public ObjectiveTestItem OptCenterXTilt { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// Y����б��(��) ������
        /// </summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; } = new ObjectiveTestItem();
    }
}
