using ColorVision.Engine.Services.Results;
using System;
using Xunit;

namespace ColorVision.UI.Tests;

public sealed class ResultMessageBusTests
{
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
