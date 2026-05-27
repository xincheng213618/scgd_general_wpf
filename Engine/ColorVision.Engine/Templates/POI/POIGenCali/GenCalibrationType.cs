using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    public enum GenCalibrationType
    {
        [Display(Name = "Engine_PG_NoCorrection", ResourceType = typeof(Properties.Resources))]
        None = -1,
        /// <summary>
        /// 只修亮度
        /// </summary>
        [Display(Name = "Engine_PG_BrightnessOnly", ResourceType = typeof(Properties.Resources))]
        BrightnessOnly,
        /// <summary>
        /// 只修色度
        /// </summary>
        [Display(Name = "Engine_PG_ChromaOnly", ResourceType = typeof(Properties.Resources))]
        ChromaOnly,
        /// <summary>
        /// 亮色度均修正
        /// </summary>
        [Display(Name = "Engine_PG_BrightnessAndChroma", ResourceType = typeof(Properties.Resources))]
        BrightnessAndChroma
    }
}
