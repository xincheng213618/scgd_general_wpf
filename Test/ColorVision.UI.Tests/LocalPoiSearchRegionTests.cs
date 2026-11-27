using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using System.IO;
using ColorVision.ImageEditor;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalPoiSearchRegionTests
{
    internal static PoiTemplateStorage ServiceTemplates(string directory, MySqlConnectedEndpoint endpoint)
    {
        var workstation = new WorkstationConfigurationSelection(WorkstationConfigurationSource.MySql, endpoint.DatabaseTarget);
        var source = new TemplateStorageSource(() => endpoint.IsCurrent, endpoint.CreateDbClient, workstation.OwnsDatabaseTarget);
        return new PoiTemplateStorage(new LocalTemplateStore(Path.Combine(directory, "local.db")), source);
    }

    [Theory]
    [InlineData(PoiShape.Rect, 1, (int)LocalLuminousAreaPoiTemplateShape.Rectangle)]
    [InlineData(PoiShape.LeftTopRect, 1, (int)LocalLuminousAreaPoiTemplateShape.LeftTopRectangle)]
    [InlineData(PoiShape.Quadrilateral, 4, (int)LocalLuminousAreaPoiTemplateShape.PolygonFour)]
    [InlineData(PoiShape.Point, 4, (int)LocalLuminousAreaPoiTemplateShape.PolygonFour)]
    public void LocalTemplateWriteFeedsEveryNamedSearchRegion(PoiShape shape, int count, int expected)
    {
        using var files = new FusionFixture();
        var storage = new PoiTemplateStorage(new LocalTemplateStore(Path.Combine(files.Directory, "local.db")), () => false);
        var template = new PoiParam { Name = "region", Width = 200, Height = 180 };
        for (int index = 0; index < count; index++)
            template.PoiPoints.Add(new PoiPoint { Name = $"PointInt{index + 1}", PointType = shape, PixWidth = 1, PixHeight = 1 });
        storage.Save(template);
        var resultEndpoint = new MySqlConnectedEndpoint(_ => true, () => throw new Exception("Unexpected result database access"));
        LocalLuminousAreaCorner[] corners =
        [
            new() { X = 11.4f, Y = 21.4f }, new() { X = 50.4f, Y = 21.4f },
            new() { X = 50.4f, Y = 51.4f }, new() { X = 11.4f, Y = 51.4f }
        ];
        var writer = new LocalFindLuminousAreaNodeServices(storage);
        Assert.Equal((LocalLuminousAreaPoiTemplateShape)expected, writer.UpdatePoiTemplate(" region ", corners, resultEndpoint));
        var region = new Int32Rect(11, 21, 40, 31);
        Assert.Equal(region, new LocalFindCrossNodeServices(storage).LoadSearchRegionTemplate("region", resultEndpoint));
        Assert.Equal(region, new LocalGridDistortionNodeServices(storage).LoadSearchRegionTemplate("region", null));
        Assert.Equal(region, new LocalRgbCrossNodeServices(storage).LoadSearchRegionTemplate("region", resultEndpoint));
        var persisted = storage.ReadLocalByName("region");
        Assert.Equal(template.Id, persisted.Id);
        Assert.Equal(200, persisted.Width);
        Assert.Equal(template.PoiPoints.Select(p => p.Name), persisted.PoiPoints.Select(p => p.Name));
        Assert.All(persisted.PoiPoints, point => Assert.Equal(shape, point.PointType));
        if (shape == PoiShape.Point)
        {
            Assert.Equal((11, 21), (persisted.PoiConfig.PointInt1.X, persisted.PoiConfig.PointInt1.Y));
            Assert.Equal((50, 51), (persisted.PoiConfig.PointInt3.X, persisted.PoiConfig.PointInt3.Y));
        }
    }

    [Fact]
    public void LocalWritesRejectAmbiguousDeletedAndUnsupportedTemplates()
    {
        using var files = new FusionFixture();
        var storage = new PoiTemplateStorage(new LocalTemplateStore(Path.Combine(files.Directory, "local.db")), () => false);
        var template = new PoiParam { Name = "region", PoiPoints = [new PoiPoint { PointType = PoiShape.Rect, PixWidth = 2, PixHeight = 2 }] };
        storage.Save(template);
        var duplicate = new PoiParam { Name = template.Name, PoiPoints = [new PoiPoint { PointType = PoiShape.Quadrilateral }] };
        storage.Save(duplicate);
        LuminousAreaPoint[] corners = [new(1, 1), new(20, 1), new(20, 20), new(1, 20)];
        void Write() => LocalLuminousAreaPoiTemplateUpdater.Update("region", corners, null, storage);
        Assert.Contains("不唯一", Assert.Throws<InvalidOperationException>(Write).Message);
        Assert.Equal(2, storage.ReadPoints(template.Id).Single().PixWidth);
        storage.Delete(template.Id);
        Assert.Throws<InvalidOperationException>(Write);
        Assert.Equal(PoiShape.Quadrilateral, storage.ReadLocalByName(template.Name).PoiPoints.Single().PointType);
        storage.Delete(duplicate.Id);
        Assert.Throws<InvalidOperationException>(Write);
        Assert.Empty(storage.Load());
    }

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
