using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterTestResult : ViewModelBase
    {
        /// <summary>
        /// XÖáÇãĞ±½Ç(¡ã) ²âÊÔÏî
        /// </summary>
        public ObjectiveTestItem ImageCenterXTilt { get; set; }

        /// <summary>
        /// YÖáÇãĞ±½Ç(¡ã) ²âÊÔÏî
        /// </summary>
        public ObjectiveTestItem ImageCenterYTilt { get; set; }

        /// <summary>
        /// Ğı×ª½Ç(¡ã) ²âÊÔÏî
        /// </summary>
        public ObjectiveTestItem ImageCenterRotation { get; set; }

        /// <summary>
        /// Ğı×ª½Ç(¡ã) ²âÊÔÏî
        /// </summary>
        public ObjectiveTestItem OptCenterRotation { get; set; }

        /// <summary>
        /// XÖáÇãĞ±½Ç(¡ã) ²âÊÔÏî
        /// </summary>
        public ObjectiveTestItem OptCenterXTilt { get; set; }

        /// <summary>
        /// YÖáÇãĞ±½Ç(¡ã) ²âÊÔÏî
        /// </summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; }
    }
}
