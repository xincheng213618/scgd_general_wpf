using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBackgroundShellDeliveryLeaseTests
{
    [Fact]
    public void DeliveryBatchesAreDetachedAndReadOnly()
    {
        var now = new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        var completion = new CopilotDeferredBackgroundShellCompletion(CreateCommandSnapshot(now), now);
        var output = new CopilotDeferredBackgroundShellOutputEvent(
            new CopilotBackgroundShellOutputMonitorEventArgs(CreateMonitorSnapshot(now), "content", 0),
            "delivery-1",
            now,
            now,
            EventBatches: 1,
            DroppedEventBatches: 0);
        var completionSource = new[] { completion };
        var outputSource = new[] { output };
        using var completionLease = new CopilotBackgroundShellCompletionDeliveryLease(null, "conversation-1", completionSource);
        using var outputLease = new CopilotBackgroundShellOutputDeliveryLease(null, "conversation-1", outputSource);

        completionSource[0] = completion with { CapturedAtUtc = now.AddMinutes(1) };
        outputSource[0] = output with { DeliveryId = "source-mutated" };

        Assert.Same(completion, Assert.Single(completionLease.Completions));
        Assert.Same(output, Assert.Single(outputLease.Events));
        AssertReadOnly(completionLease.Completions, completion with { CapturedAtUtc = now.AddMinutes(2) });
        AssertReadOnly(outputLease.Events, output with { DeliveryId = "replacement" });
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> values, T replacement)
    {
        var items = Assert.IsAssignableFrom<IList<T>>(values);
        Assert.True(items.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => items[0] = replacement);
    }

    private static CopilotBackgroundShellCommandSnapshot CreateCommandSnapshot(DateTimeOffset now)
        => new(
            "background-1",
            "conversation-1",
            "task-1",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "Write-Output test",
            "sha256",
            now,
            now,
            123,
            ProcessTreeContained: true,
            CopilotBackgroundShellCommandState.Completed,
            0,
            "output",
            string.Empty);

    private static CopilotBackgroundShellOutputMonitorSnapshot CreateMonitorSnapshot(DateTimeOffset now)
        => new(
            "monitor-1",
            "conversation-1",
            "background-1",
            CopilotBackgroundShellOutputStream.StandardOutput,
            "Observe output",
            now,
            now.AddMinutes(5),
            CopilotBackgroundShellOutputMonitorState.Running,
            PublishedEvents: 1,
            SuppressedEvents: 0);
}
