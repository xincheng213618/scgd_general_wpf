using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using FlowEngineLib.Node.POI;
using System;
using System.Linq;
using Xunit;

namespace ColorVision.UI.Tests;

public class LocalFlowNodePortTests
{
    private static readonly string[] CombinedInputNames = { "IN_IMG", "IN_POI" };
    private static readonly string[] RealPoiInputNames = { "IN_CIE", "IN_POI" };
    private static readonly string[] SingleInputName = { "IN" };

    [Fact]
    public void LocalCalibrationRealPoiNodeUsesImageAndPoiInputs()
    {
        LocalCalibrationRealPoiNode node = new();

        node.Create();

        Assert.Equal(CombinedInputNames, node.GetAllInputOptions().Select(option => option.Text));
        Assert.NotNull(typeof(LocalCalibrationRealPoiNode).GetProperty(nameof(LocalCalibrationRealPoiNode.ImageFilePath)));
    }

    [Fact]
    public void LocalCalibrationNodeUsesOnlyCurrentFrame()
    {
        LocalCalibrationNode node = new();

        node.Create();

        Assert.Equal(SingleInputName, node.GetAllInputOptions().Select(option => option.Text));
        Assert.Null(typeof(LocalCalibrationNode).GetProperty("ImageFilePath"));
    }

    [Fact]
    public void LocalRealPoiNodeUsesCieAndPoiInputsWithoutImageProperty()
    {
        LocalRealPoiNode node = new();

        node.Create();

        Assert.Equal(RealPoiInputNames, node.GetAllInputOptions().Select(option => option.Text));
        Assert.Null(typeof(LocalRealPoiNode).GetProperty("ImageFilePath"));
    }

    [Fact]
    public void LocalBuildPoiNodesKeepSingleInputAndHaveNoImageProperty()
    {
        LocalBuildPoiNode remappingNode = new();
        LocalBuildPoiByTemplateNode parameterNode = new();

        remappingNode.Create();
        parameterNode.Create();

        Assert.Equal(SingleInputName, remappingNode.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(SingleInputName, parameterNode.GetAllInputOptions().Select(option => option.Text));
        Assert.Null(typeof(LocalBuildPoiNode).GetProperty("ImgFileName"));
        Assert.Null(typeof(LocalBuildPoiByTemplateNode).GetProperty("ImgFileName"));
        Assert.Equal("POI_W_AUTO", remappingNode.LayoutROITemplateName);
        Assert.Equal("POI_W_AUTO", parameterNode.LayoutROITemplateName);
        Assert.Equal(typeof(FlowBuildPoiTemplateEditor), FlowNodePropertyEditorAttribute.Resolve(
            typeof(LocalBuildPoiByTemplateNode),
            nameof(LocalBuildPoiByTemplateNode.ParameterTemplateName)));
    }

    [Fact]
    public void LocalBuildPoiNodesHideDeviceCode()
    {
        Assert.False(FlowNodePropertyMetadataProvider.Instance.IsBrowsable(
            typeof(LocalBuildPoiNode).GetProperty(nameof(CVBaseServerNode.DeviceCode))!));
        Assert.False(FlowNodePropertyMetadataProvider.Instance.IsBrowsable(
            typeof(LocalBuildPoiByTemplateNode).GetProperty(nameof(CVBaseServerNode.DeviceCode))!));
    }

    [Fact]
    public void LocalParameterBuildPoiUsesTemplateAndRoiWithoutImage()
    {
        ParamBuildPoi parameter = new()
        {
            POILayout = CVCommCore.CVAlgorithm.POILayoutTypes.PolygonFour,
            LayoutRows = 2,
            LayoutCols = 2,
            MarginType = ColorVision.ImageEditor.GraphicBorderType.Absolute,
            MarginLeft = 0,
            MarginTop = 0,
            MarginRight = 0,
            MarginBottom = 0,
            PointType = CVCommCore.CVAlgorithm.POIPointTypes.Rect,
            PointWidth = 8,
            PointHeight = 6
        };
        PoiParam layout = new() { Name = "POI_W_AUTO" };
        layout.PoiPoints.Add(new PoiPoint { PixX = 10, PixY = 20 });
        layout.PoiPoints.Add(new PoiPoint { PixX = 110, PixY = 20 });
        layout.PoiPoints.Add(new PoiPoint { PixX = 110, PixY = 120 });
        layout.PoiPoints.Add(new PoiPoint { PixX = 10, PixY = 120 });

        LocalPoiRemappedPoint[] points = LocalPoiLayoutCalculator.Build(parameter, layout).ToArray();

        Assert.Equal(4, points.Length);
        Assert.Equal(new[] { 10, 110, 10, 110 }, points.Select(point => point.X));
        Assert.Equal(new[] { 20, 20, 120, 120 }, points.Select(point => point.Y));
        Assert.All(points, point =>
        {
            Assert.Equal(CVCommCore.CVAlgorithm.POIPointTypes.Rect, point.PointType);
            Assert.Equal(8, point.Width);
            Assert.Equal(6, point.Height);
        });
    }

    [Fact]
    public void LocalPoiRemappingMapsTemplateWithoutImage()
    {
        PoiParam template = new()
        {
            Name = "W_Adapt_POI",
            LeftTopX = 0,
            LeftTopY = 0,
            RightTopX = 100,
            RightTopY = 0,
            RightBottomX = 100,
            RightBottomY = 100,
            LeftBottomX = 0,
            LeftBottomY = 100
        };
        template.PoiPoints.Add(new PoiPoint
        {
            Id = 7,
            Name = "Center",
            PointType = PoiShape.Circle,
            PixX = 50,
            PixY = 50,
            PixWidth = 10,
            PixHeight = 10
        });
        LocalPoiMappingPoint[] layout =
        {
            new(10, 20),
            new(210, 20),
            new(210, 220),
            new(10, 220)
        };

        LocalPoiRemappedPoint point = Assert.Single(LocalPoiRemappingCalculator.Remap(template, layout, "P_"));

        Assert.Equal(7, point.PoiId);
        Assert.Equal("P_Center", point.Name);
        Assert.Equal(CVCommCore.CVAlgorithm.POIPointTypes.Circle, point.PointType);
        Assert.Equal(110, point.X);
        Assert.Equal(120, point.Y);
        Assert.Equal(20, point.Width);
        Assert.Equal(20, point.Height);
    }

    [Fact]
    public void LocalPoiRemappingTruncatesCoordinatesLikeNativeService()
    {
        PoiParam template = new()
        {
            Name = "Fractional",
            LeftTopX = 0,
            LeftTopY = 0,
            RightTopX = 10,
            RightTopY = 0,
            RightBottomX = 10,
            RightBottomY = 10,
            LeftBottomX = 0,
            LeftBottomY = 10
        };
        template.PoiPoints.Add(new PoiPoint
        {
            Id = 1,
            Name = "Point",
            PointType = PoiShape.Point,
            PixX = 5,
            PixY = 5,
            PixWidth = 1,
            PixHeight = 1
        });
        LocalPoiMappingPoint[] layout =
        {
            new(0, 0),
            new(11, 0),
            new(11, 11),
            new(0, 11)
        };

        LocalPoiRemappedPoint point = Assert.Single(LocalPoiRemappingCalculator.Remap(template, layout, null));

        Assert.Equal(5, point.X);
        Assert.Equal(5, point.Y);
    }

    [Fact]
    public void LocalCalibrationRealPoiNodeMatchesServicePoiSizeRules()
    {
        LocalCalibrationRealPoiNode node = new()
        {
            POIType = POIPointTypes.Circle,
            POIWidth = 11
        };

        Assert.Equal(12, node.POIWidth);
        Assert.Equal(node.POIWidth, node.POIHeight);
    }

    [Fact]
    public void LocalRealPoiNodeMatchesServicePoiSizeRules()
    {
        LocalRealPoiNode node = new()
        {
            POIType = POIPointTypes.Circle,
            POIWidth = 11
        };

        Assert.Equal(12, node.POIWidth);
        Assert.Equal(node.POIWidth, node.POIHeight);
    }

    [Theory]
    [InlineData(3, ViewResultAlgType.POI_XYZ)]
    [InlineData(1, ViewResultAlgType.POI_Y)]
    public void LocalPoiResultTypeMatchesCieChannels(int channels, ViewResultAlgType expected)
    {
        Assert.Equal(expected, LocalPoiCalculator.ResolveResultType(channels));
    }

    [Fact]
    public void LocalFrameLivesAcrossNodeCopiesAndEndsWithFlow()
    {
        CVStartCFC action = new("local-frame-lifetime");
        LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 2,
            Height = 2,
            SourceBpp = 8,
            Channels = 1,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw
        }, 4, 0);
        action.SetCurrentFrame(frame);
        CVStartCFC downstream = new(action);

        Assert.True(downstream.TryGetCurrentFrame(out LocalFlowFrame? sharedFrame));
        Assert.Same(frame, sharedFrame);
        using (LocalFlowFrameLease lease = sharedFrame!.Acquire())
        {
            Assert.NotEqual(IntPtr.Zero, lease.RawPointer);
        }

        downstream.DoFinishing();

        Assert.Throws<ObjectDisposedException>(() => frame.Acquire());
    }
}
