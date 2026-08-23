using System.Runtime.InteropServices;
using ColorVision.Core;

namespace ColorVision.UI.Tests;

public class HImageAbiTests
{
    [Fact]
    public void RoiRectKeepsPackedNativeLayout()
    {
        Assert.Equal(LayoutKind.Sequential, typeof(RoiRect).StructLayoutAttribute?.Value);
        Assert.Equal(1, typeof(RoiRect).StructLayoutAttribute?.Pack);
        Assert.Equal(16, Marshal.SizeOf<RoiRect>());
        Assert.Equal(0, Marshal.OffsetOf<RoiRect>(nameof(RoiRect.X)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<RoiRect>(nameof(RoiRect.Y)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<RoiRect>(nameof(RoiRect.Width)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<RoiRect>(nameof(RoiRect.Height)).ToInt32());
    }

    [Fact]
    public void HImageKeepsExplicitX64NativeLayout()
    {
        Assert.Equal(LayoutKind.Sequential, typeof(HImage).StructLayoutAttribute?.Value);
        Assert.Equal(8, typeof(HImage).StructLayoutAttribute?.Pack);
        Assert.Equal(32, Marshal.SizeOf<HImage>());
        Assert.Equal(0, Marshal.OffsetOf<HImage>(nameof(HImage.rows)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<HImage>(nameof(HImage.cols)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<HImage>(nameof(HImage.channels)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<HImage>(nameof(HImage.depth)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<HImage>(nameof(HImage.stride)).ToInt32());
        Assert.Equal(20, Marshal.OffsetOf<HImage>(nameof(HImage.isDispose)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<HImage>(nameof(HImage.pData)).ToInt32());
    }
}
