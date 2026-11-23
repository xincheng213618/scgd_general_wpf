using System.Windows.Media;
using static MvCamCtrl.NET.MyCamera.MvGvspPixelType;
using MvGvspPixelType = MvCamCtrl.NET.MyCamera.MvGvspPixelType;

namespace Conoscope.MVS;

internal static class MvsPreviewPixelFormat
{
    internal static PixelFormat? GetDirectFormat(MvGvspPixelType source, bool convertBayer) => source switch
    {
        PixelType_Gvsp_Mono8 => PixelFormats.Gray8,
        PixelType_Gvsp_RGB8_Packed => PixelFormats.Rgb24,
        PixelType_Gvsp_BGR8_Packed => PixelFormats.Bgr24,
        PixelType_Gvsp_BayerGR8 or PixelType_Gvsp_BayerRG8 or PixelType_Gvsp_BayerGB8 or PixelType_Gvsp_BayerBG8
            when !convertBayer => PixelFormats.Gray8,
        _ => null,
    };
}
