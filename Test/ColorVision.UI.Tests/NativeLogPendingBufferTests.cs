#pragma warning disable CA1707
using ColorVision.NativeLogging;

namespace ColorVision.UI.Tests;

public sealed class NativeLogPendingBufferTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeLogPendingBuffer(0));
    }

    [Fact]
    public void Enqueue_RespectsCapacityAndKeepsNewestEntries()
    {
        NativeLogPendingBuffer buffer = new(capacity: 3);

        Assert.True(buffer.Enqueue(CreateEntry(1)));
        Assert.True(buffer.Enqueue(CreateEntry(2)));
        Assert.True(buffer.Enqueue(CreateEntry(3)));
        Assert.False(buffer.Enqueue(CreateEntry(4)));

        NativeLogDrainBatch batch = buffer.Drain(maxEntries: 10);

        Assert.Equal(3, buffer.Capacity);
        Assert.Equal(1, batch.DroppedCount);
        Assert.Equal(0, batch.RemainingCount);
        Assert.Equal(["entry-2", "entry-3", "entry-4"], batch.Entries.Select(entry => entry.Message));
    }

    [Fact]
    public void Drain_ReturnsFifoBatchesAndReportsRemainingCount()
    {
        NativeLogPendingBuffer buffer = new(capacity: 5);
        for (int i = 1; i <= 5; i++)
        {
            buffer.Enqueue(CreateEntry(i));
        }

        NativeLogDrainBatch first = buffer.Drain(maxEntries: 2);
        NativeLogDrainBatch second = buffer.Drain(maxEntries: 2);
        NativeLogDrainBatch third = buffer.Drain(maxEntries: 2);

        Assert.Equal(["entry-1", "entry-2"], first.Entries.Select(entry => entry.Message));
        Assert.Equal(3, first.RemainingCount);
        Assert.Equal(["entry-3", "entry-4"], second.Entries.Select(entry => entry.Message));
        Assert.Equal(1, second.RemainingCount);
        Assert.Equal(["entry-5"], third.Entries.Select(entry => entry.Message));
        Assert.Equal(0, third.RemainingCount);
    }

    [Fact]
    public void Clear_RemovesPendingEntriesAndResetsDroppedCount()
    {
        NativeLogPendingBuffer buffer = new(capacity: 1);
        buffer.Enqueue(CreateEntry(1));
        buffer.Enqueue(CreateEntry(2));

        buffer.Clear();

        Assert.Equal(new NativeLogBufferSnapshot(0, 0), buffer.GetSnapshot());
        Assert.Empty(buffer.Drain(1).Entries);
    }

    [Fact]
    public void Session_IsOffUntilStartedAndPauseOnlyFreezesDisplayDrain()
    {
        FakeCaptureController controller = new();
        using NativeLogWindowSession session = new(controller, pendingCapacity: 4);

        controller.Publish(CreateEntry(1));
        Assert.Equal(0, session.GetBufferSnapshot().PendingCount);

        Assert.True(session.Start(NativeLogSeverity.Debug).Success);
        controller.Publish(CreateEntry(2));
        session.IsPaused = true;
        controller.Publish(CreateEntry(3));

        NativeLogDrainBatch paused = session.Drain(maxEntries: 4);
        Assert.Empty(paused.Entries);
        Assert.Equal(2, paused.RemainingCount);

        session.IsPaused = false;
        Assert.Equal(["entry-2", "entry-3"], session.Drain(4).Entries.Select(entry => entry.Message));

        session.Stop();
        controller.Publish(CreateEntry(4));
        Assert.Equal(0, session.GetBufferSnapshot().PendingCount);
    }

    [Fact]
    public void Session_CapturesMessagesRaisedWhileControllerStarts()
    {
        FakeCaptureController controller = new()
        {
            PublishOnStart = CreateEntry(7),
        };
        using NativeLogWindowSession session = new(controller, pendingCapacity: 4);

        Assert.True(session.Start(NativeLogSeverity.Info).Success);

        NativeLogDrainBatch batch = session.Drain(4);
        Assert.Single(batch.Entries);
        Assert.Equal("entry-7", batch.Entries[0].Message);
    }

    private static NativeLogDisplayEntry CreateEntry(int id)
    {
        return new NativeLogDisplayEntry(
            DateTimeOffset.UnixEpoch.AddSeconds(id),
            id,
            "opencv_helper",
            NativeLogSeverity.Info,
            $"entry-{id}");
    }

    private sealed class FakeCaptureController : INativeLogCaptureController
    {
        public event Action<NativeLogDisplayEntry>? LogReceived;

        public bool IsEnabled { get; private set; }

        public NativeLogDisplayEntry? PublishOnStart { get; init; }

        public NativeLogOperationResult Start(NativeLogSeverity level)
        {
            IsEnabled = true;
            if (PublishOnStart is NativeLogDisplayEntry entry)
            {
                LogReceived?.Invoke(entry);
            }
            return NativeLogOperationResult.Succeeded();
        }

        public NativeLogOperationResult SetLevel(NativeLogSeverity level)
        {
            return NativeLogOperationResult.Succeeded();
        }

        public void Stop()
        {
            IsEnabled = false;
        }

        public void Publish(NativeLogDisplayEntry entry)
        {
            LogReceived?.Invoke(entry);
        }

        public void Dispose()
        {
        }
    }
}
