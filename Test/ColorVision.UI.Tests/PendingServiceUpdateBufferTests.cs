using ColorVision.Engine.Services.RC;
using MQTTMessageLib;

namespace ColorVision.UI.Tests;

public class PendingServiceUpdateBufferTests
{
    [Fact]
    public void Take_ReturnsBothSnapshotsAndClearsBuffer()
    {
        PendingServiceUpdateBuffer buffer = new();
        Dictionary<CVServiceType, List<MQTTNodeService>> services = new()
        {
            [CVServiceType.Camera] = new List<MQTTNodeService>()
        };
        List<MQTTNodeServiceStatus> statuses = new();

        buffer.StoreServices(services);
        buffer.StoreStatuses(statuses);

        PendingServiceUpdates updates = buffer.Take();
        PendingServiceUpdates empty = buffer.Take();

        Assert.Same(services, updates.Services);
        Assert.Same(statuses, updates.Statuses);
        Assert.Null(empty.Services);
        Assert.Null(empty.Statuses);
    }

    [Fact]
    public void StoreServices_KeepsNewestSnapshotWithoutDiscardingStatuses()
    {
        PendingServiceUpdateBuffer buffer = new();
        Dictionary<CVServiceType, List<MQTTNodeService>> first = new();
        Dictionary<CVServiceType, List<MQTTNodeService>> latest = new();
        List<MQTTNodeServiceStatus> statuses = new();

        buffer.StoreServices(first);
        buffer.StoreStatuses(statuses);
        buffer.StoreServices(latest);

        PendingServiceUpdates updates = buffer.Take();

        Assert.Same(latest, updates.Services);
        Assert.Same(statuses, updates.Statuses);
    }
}
