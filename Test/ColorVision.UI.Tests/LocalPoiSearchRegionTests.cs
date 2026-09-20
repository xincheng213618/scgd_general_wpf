using ColorVision.Core;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalPoiSearchRegionTests
{
    [Theory]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.RectPointType, 1)]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType, 1)]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType, 4)]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType, 4)]
    public void LuminousWriterRoundTripsAllSupportedShapesWithoutCenterOffset(int type, int count)
    {
        PoiDetailModel[] source = Enumerable.Range(0, count).Select(i => new PoiDetailModel { Id = i, Type = (GraphicTypes)type }).ToArray();
        LuminousAreaPoint[] corners = [new(11, 21), new(50, 21), new(50, 51), new(11, 51)];
        var update = LocalLuminousAreaPoiTemplateUpdater.BuildUpdate(source, corners);
        Assert.Equal(new Int32Rect(11, 21, 40, 31), LocalPoiSearchRegionResolver.Resolve(update.Details, "发光区"));
    }

    [Fact]
    public void RotatedCornersUseInclusiveBoundingRectangle()
    {
        LuminousAreaPoint[] corners = [new(20, 10), new(40, 20), new(30, 40), new(10, 30)];
        PoiDetailModel[] details = corners.Select(p => new PoiDetailModel
        {
            Type = (GraphicTypes)LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType,
            PixX = (int)p.X, PixY = (int)p.Y
        }).ToArray();
        Assert.Equal(new Int32Rect(10, 10, 31, 31), LocalPoiSearchRegionResolver.Resolve(details, "倾斜发光区"));
        (details[1], details[2]) = (details[2], details[1]);
        Assert.Throws<InvalidOperationException>(() => LocalPoiSearchRegionResolver.Resolve(details, "无效顺序"));
    }

    [Fact]
    public void EmptyOrInvalidTemplateCannotBecomeFullImage()
    {
        Assert.Throws<InvalidOperationException>(() => LocalPoiSearchRegionResolver.Resolve([], "空模板"));
        Assert.Throws<InvalidOperationException>(() => LocalPoiSearchRegionResolver.Resolve([new PoiDetailModel
        {
            Type = (GraphicTypes)LocalLuminousAreaPoiTemplateUpdater.RectPointType,
            PixX = 0, PixY = 0, PixWidth = 0, PixHeight = 0
        }], "无效矩形"));
        Assert.Throws<InvalidOperationException>(() => LocalPoiSearchRegionResolver.Resolve([new PoiDetailModel
        {
            Type = (GraphicTypes)LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType,
            PixX = null, PixY = 0, PixWidth = 30, PixHeight = 20
        }], "缺少坐标"));
    }
}
