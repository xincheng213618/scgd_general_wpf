using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class FlowPackagePoiCodecTests
{
    [Fact]
    public void CreatePortableSnapshot_NormalizesIdsAndPreservesBusinessData()
    {
        PoiParam source = new()
        {
            Id = 42,
            Name = "POI-A",
            Width = 1920,
            Height = 1080,
            Type = 3,
            LeftTopX = 10,
            PoiConfig = new PoiConfig
            {
                BackgroundFilePath = "background.png",
                DefaultPointType = GraphicTypes.Circle,
                DefaultCircleRadius = 27
            }
        };
        source.PoiPoints.Add(
            new PoiPoint
            {
                Id = 100,
                Name = "stale-memory-point",
                PixX = 1
            });
        PoiPoint persistedPoint = new()
        {
            Id = 200,
            Name = "persisted-point",
            PointType = PoiShape.Rect,
            PixX = 12,
            PixY = 34,
            PixWidth = 56,
            PixHeight = 78
        };

        PoiParam snapshot = TemplatePoi.CreatePortableSnapshot(
            source,
            new[] { persistedPoint });

        Assert.Equal(-1, snapshot.Id);
        PoiPoint point = Assert.Single(snapshot.PoiPoints);
        Assert.Equal(-1, point.Id);
        Assert.Equal("persisted-point", point.Name);
        Assert.Equal(PoiShape.Rect, point.PointType);
        Assert.Equal(12, point.PixX);
        Assert.Equal(34, point.PixY);
        Assert.Equal(56, point.PixWidth);
        Assert.Equal(78, point.PixHeight);
        Assert.Equal("POI-A", snapshot.Name);
        Assert.Equal(1920, snapshot.Width);
        Assert.Equal(1080, snapshot.Height);
        Assert.Equal(3, snapshot.Type);
        Assert.Equal(10, snapshot.LeftTopX);
        Assert.Equal("background.png", snapshot.PoiConfig.BackgroundFilePath);
        Assert.Equal(27, snapshot.PoiConfig.DefaultCircleRadius);
        Assert.NotSame(source, snapshot);
        Assert.NotSame(source.PoiConfig, snapshot.PoiConfig);

        Assert.Equal(42, source.Id);
        Assert.Equal(100, Assert.Single(source.PoiPoints).Id);
        Assert.Equal(200, persistedPoint.Id);
    }

    [Fact]
    public void PrepareImport_DoesNotReusePackagedDatabaseIds()
    {
        PoiParam packaged = new()
        {
            Id = 500,
            Name = "source-name",
            Width = 800,
            Height = 600
        };
        packaged.PoiPoints.Add(
            new PoiPoint
            {
                Id = 501,
                Name = "center",
                PixX = 400,
                PixY = 300
            });
        TemplatePoi codec = new();
        try
        {
            bool prepared = codec.TryPrepareFlowPackageImport(
                "imported-name",
                JsonConvert.SerializeObject(packaged));

            Assert.True(prepared);
            PoiParam imported = Assert.IsType<PoiParam>(codec.ImportTemp);
            Assert.Equal(-1, imported.Id);
            Assert.Equal("imported-name", imported.Name);
            Assert.Equal(-1, Assert.Single(imported.PoiPoints).Id);
            Assert.Equal(800, imported.Width);
            Assert.Equal(600, imported.Height);
        }
        finally
        {
            codec.ClearCreateTemplateSource();
            if (TemplateControl.ITemplateNames.TryGetValue(
                    codec.Name,
                    out ITemplate? registered)
                && ReferenceEquals(registered, codec))
            {
                TemplateControl.ITemplateNames.Remove(codec.Name);
            }
        }
    }

    [Fact]
    public void CreatePortableSnapshot_PreservesAuthoritativePointOrder()
    {
        PoiParam source = new() { Id = 7, Name = "ordered" };
        PoiPoint first = new() { Id = 11, Name = "first" };
        PoiPoint second = new() { Id = 12, Name = "second" };

        PoiParam snapshot = TemplatePoi.CreatePortableSnapshot(
            source,
            new[] { first, second });

        Assert.Equal(
            new[] { "first", "second" },
            snapshot.PoiPoints.Select(point => point.Name));
        Assert.All(snapshot.PoiPoints, point => Assert.Equal(-1, point.Id));
    }
}
