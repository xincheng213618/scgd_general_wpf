using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Flow.Nodes;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine;
using FlowEngineLib.Base;
using FlowEngineLib.Node.POI;
using System;
using System.Linq;
using Xunit;

namespace ColorVision.UI.Tests;

public class LocalFlowNodePortTests
{
    private static readonly string[] CombinedInputNames = { "IN_IMG", "IN_POI" };
    private static readonly string[] SingleInputName = { "IN" };

    [Fact]
    public void LocalCalibrationRealPoiNodeUsesImageAndPoiInputs()
    {
        LocalCalibrationRealPoiNode node = new();

        node.Create();

        Assert.Equal(CombinedInputNames, node.GetAllInputOptions().Select(option => option.Text));
    }

    [Fact]
    public void LocalCalibrationNodeKeepsSingleInput()
    {
        LocalCalibrationNode node = new();

        node.Create();

        Assert.Equal(SingleInputName, node.GetAllInputOptions().Select(option => option.Text));
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
