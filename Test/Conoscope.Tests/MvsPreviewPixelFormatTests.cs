using Conoscope.MVS;
using System.Windows.Media;
using MvsPixelType = MvCamCtrl.NET.MyCamera.MvGvspPixelType;

namespace Conoscope.Tests;

public sealed class MvsPreviewPixelFormatTests
{
    [Fact]
    public void PackedEightBitFramesUseTheirNativeChannelOrder()
    {
        Assert.Equal<PixelFormat?>(PixelFormats.Gray8, MvsPreviewPixelFormat.GetDirectFormat(MvsPixelType.PixelType_Gvsp_Mono8, true));
        Assert.Equal<PixelFormat?>(PixelFormats.Rgb24, MvsPreviewPixelFormat.GetDirectFormat(MvsPixelType.PixelType_Gvsp_RGB8_Packed, true));
        Assert.Equal<PixelFormat?>(PixelFormats.Bgr24, MvsPreviewPixelFormat.GetDirectFormat(MvsPixelType.PixelType_Gvsp_BGR8_Packed, true));
    }

    [Theory]
    [InlineData(MvsPixelType.PixelType_Gvsp_BayerGR8)]
    [InlineData(MvsPixelType.PixelType_Gvsp_BayerRG8)]
    [InlineData(MvsPixelType.PixelType_Gvsp_BayerGB8)]
    [InlineData(MvsPixelType.PixelType_Gvsp_BayerBG8)]
    public void EightBitBayerUsesRawGrayscaleOrDemosaicingAccordingToSettings(MvsPixelType source)
    {
        Assert.Equal<PixelFormat?>(PixelFormats.Gray8, MvsPreviewPixelFormat.GetDirectFormat(source, false));
        Assert.Null(MvsPreviewPixelFormat.GetDirectFormat(source, true));
    }

    [Theory]
    [InlineData(MvsPixelType.PixelType_Gvsp_Mono12)]
    [InlineData(MvsPixelType.PixelType_Gvsp_YUV422_Packed)]
    public void OtherFrameLayoutsRequireConversion(MvsPixelType source)
    {
        Assert.Null(MvsPreviewPixelFormat.GetDirectFormat(source, false));
    }
}
