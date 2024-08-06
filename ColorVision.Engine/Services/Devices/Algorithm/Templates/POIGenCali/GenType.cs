#pragma warning disable IDE1006
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIGenCali
{
    public enum GenType
    {
        /// <summary>
        /// 差值
        /// </summary>
        [Description("差值")]
        Difference,
        /// <summary>
        /// 比例
        /// </summary>
        [Description("比例")]
        Ratio
    }

    public enum GenCalibrationType
    {
        /// <summary>
        /// 只修亮度
        /// </summary>
        [Description("只修亮度")]
        BrightnessOnly,
        /// <summary>
        /// 只修色度
        /// </summary>
        [Description("只修色度")]
        ChromaOnly,
        /// <summary>
        /// 亮色度均修正
        /// </summary>
        [Description("亮色度均修正")]
        BrightnessAndChroma 
    }
}
