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
        public ObjectiveTestItem OptCenterXTilt { get; set; } = new ObjectiveTestItem() { Name = "Tilt_x", Unit = "degree" };

        /// <summary>
        /// Y÷·«„–±Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; } = new ObjectiveTestItem() { Name = "Tilt_y", Unit = "degree" };
        /// <summary>
        /// –˝◊™Ω«(°„) ≤‚ ‘œÓ
        /// </summary>
        public ObjectiveTestItem OptCenterRotation { get; set; } = new ObjectiveTestItem() { Name = "Rotation", Unit = "degree" };

    }
}
