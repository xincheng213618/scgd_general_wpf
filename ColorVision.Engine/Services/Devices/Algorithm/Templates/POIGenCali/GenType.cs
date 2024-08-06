#pragma warning disable IDE1006
namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIGenCali
{
    public enum GenType
    {
        Difference, // 差值
        Ratio       // 比例
    }

    public enum GenCalibrationType
    {
        BrightnessOnly,  // 只修亮度
        ChromaOnly,      // 只修色度
        BrightnessAndChroma // 亮色度均修正
    }
}
