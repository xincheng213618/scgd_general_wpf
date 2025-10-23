using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterTestResult : ViewModelBase
    {
        /// <summary>
        /// X÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem ImageCenterXTilt { get; set; }

        /// <summary>
        /// Y÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem ImageCenterYTilt { get; set; }

        /// <summary>
        /// –˝◊™Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem ImageCenterRotation { get; set; }

        /// <summary>
        /// –˝◊™Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterRotation { get; set; }

        /// <summary>
        /// X÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterXTilt { get; set; }

        /// <summary>
        /// Y÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; }
    }
}
