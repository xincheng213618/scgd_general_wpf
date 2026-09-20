using ColorVision.Engine.Services.Results;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Images.FileFusion;
using ColorVision.Engine;
using FlowEngineLib.Base;
using System;
using Xunit;

namespace ColorVision.UI.Tests;

public sealed class ResultMessageBusTests
{
    [Theory]
    [InlineData("cross")]
    [InlineData("grid")]
    [InlineData("fov")]
    [InlineData("luminous")]
    [InlineData("fusion")]
    public void LocalAlgorithmsPublishByBatchAndNodeWithoutDevice(string algorithm)
    {
        string nodeId = Guid.NewGuid().ToString("N");
        ResultMessage? received = null;
        using IDisposable subscription = ResultMessageBus.Default.Subscribe(message =>
        {
            if (message.NodeId == nodeId) received = message;
        });
        switch (algorithm)
        {
            case "cross":
                LocalFindCrossNodeServices.Instance.Publish(new() { NodeId = nodeId, SerialNumber = "batch", MasterId = 42, ZIndex = 3, OperatorCode = "FindCross" });
                break;
            case "grid":
                LocalGridDistortionNodeServices.Instance.Publish(new() { NodeId = nodeId, SerialNumber = "batch", MasterId = 42, ZIndex = 3 });
                break;
            case "fov":
                LocalFovNodeServices.Instance.Publish(new() { NodeId = nodeId, SerialNumber = "batch", MasterId = 42, ZIndex = 3 });
                break;
            case "luminous":
                LocalFindLuminousAreaNodeServices.Instance.Publish(new() { NodeId = nodeId, SerialNumber = "batch", MasterId = 42, ZIndex = 3, OperatorCode = "FindLightArea" });
                break;
            case "fusion":
                FileFusionServices.Instance.Publish(new CVStartCFC("batch"), nodeId, 3, new MeasureResultImgModel { Id = 42 });
                break;
        }
        Assert.NotNull(received);
        Assert.Equal(ResultRoutes.LocalFlow, received.Route);
        Assert.Empty(received.DeviceCode);
        Assert.Equal("batch", received.SerialNumber);
        Assert.Equal(3, received.ZIndex);
        Assert.Equal(42, received.Data.MasterId);
        Assert.Equal(algorithm == "fusion" ? ResultKinds.Image : ResultKinds.Algorithm, received.ResultKind);
    }

    [Fact]
    public void PersistedMessageUsesVersionedStandardEnvelope()
    {
        ResultMessageBus bus = new();
        ResultMessage? received = null;
        using IDisposable subscription = bus.Subscribe(message => received = message);

        bus.PublishPersisted(ResultRoutes.Camera, ResultKinds.Image, "camera-1", "GetData", "batch-1", "node-1", 3, 42, 100);

        Assert.NotNull(received);
        Assert.Equal(ResultMessage.CurrentProtocolVersion, received.ProtocolVersion);
        Assert.Equal(ResultRoutes.Camera, received.Route);
        Assert.Equal(ResultKinds.Image, received.ResultKind);
        Assert.Equal("camera-1", received.DeviceCode);
        Assert.Equal("batch-1", received.SerialNumber);
        Assert.Equal(42, received.Data.MasterId);
        Assert.Equal(100, received.Data.MasterResultType);
        Assert.Null(received.Attachment);
    }

    [Fact]
    public void DisposedSubscriptionStopsMessages()
    {
        ResultMessageBus bus = new();
        int count = 0;
        IDisposable subscription = bus.Subscribe(_ => count++);

        bus.PublishPersisted(ResultRoutes.Algorithm, ResultKinds.Algorithm, "algorithm-1", "Calculate", "batch-1", "node-1", -1, 7, 301);
        subscription.Dispose();
        bus.PublishPersisted(ResultRoutes.Algorithm, ResultKinds.Algorithm, "algorithm-1", "Calculate", "batch-2", "node-1", -1, 8, 301);

        Assert.Equal(1, count);
    }
}
