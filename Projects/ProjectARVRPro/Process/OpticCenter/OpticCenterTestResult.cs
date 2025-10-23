using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterTestResult : ViewModelBase
    {
        /// <summary>
        /// X����б��(��) ������
        /// </summary>
        public ObjectiveTestItem ImageCenterXTilt { get; set; }

        /// <summary>
        /// Y����б��(��) ������
        /// </summary>
        public ObjectiveTestItem ImageCenterYTilt { get; set; }

        /// <summary>
        /// ��ת��(��) ������
        /// </summary>
        public ObjectiveTestItem ImageCenterRotation { get; set; }

        /// <summary>
        /// ��ת��(��) ������
        /// </summary>
        public ObjectiveTestItem OptCenterRotation { get; set; }

        /// <summary>
        /// X����б��(��) ������
        /// </summary>
        public ObjectiveTestItem OptCenterXTilt { get; set; }

        /// <summary>
        /// Y����б��(��) ������
        /// </summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; }
    }
}
