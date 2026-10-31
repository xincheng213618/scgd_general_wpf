using ColorVision.Common.MVVM;
using ProjectARVRPro.Process.KeyedResults;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterTestResult : ViewModelBase
    {
        public string Key { get; set; } = OpticCenterProcessConfig.DefaultKey;

        public string GetOutputKey() => KeyedTestResultDictionary.NormalizeKey(Key, OpticCenterProcessConfig.DefaultKey);

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
