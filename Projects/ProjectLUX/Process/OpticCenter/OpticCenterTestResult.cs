using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Windows; // PoiPointResultModel

namespace ProjectLUX.Process.OpticCenter
{
    public class OpticCenterTestResult : ViewModelBase
    {
        /// <summary>
        /// X÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem ImageCenterXTilt { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// Y÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem ImageCenterYTilt { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// –˝◊™Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem ImageCenterRotation { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// –˝◊™Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterRotation { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// X÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterXTilt { get; set; } = new ObjectiveTestItem();

        /// <summary>
        /// Y÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; } = new ObjectiveTestItem();
    }
}
