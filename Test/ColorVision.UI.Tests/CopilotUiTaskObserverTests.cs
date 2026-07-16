#pragma warning disable CA1707
using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotUiTaskObserverTests
{
    [Fact]
    public async Task ObserveAsync_ContainsSynchronousAndAsynchronousFailuresWithSanitizedFeedback()
    {
        var failures = new List<string>();

        await CopilotUiTaskObserver.ObserveAsync(
            () => throw new InvalidOperationException("password=sync-secret"),
            "sync operation",
            failures.Add);
        await CopilotUiTaskObserver.ObserveAsync(
            () => Task.FromException(new IOException("Bearer async-secret")),
            "async operation",
            failures.Add);

        Assert.Equal(2, failures.Count);
        Assert.Contains("password=<redacted>", failures[0], StringComparison.Ordinal);
        Assert.Contains("Bearer <redacted>", failures[1], StringComparison.Ordinal);
        Assert.DoesNotContain("sync-secret", string.Join(" ", failures), StringComparison.Ordinal);
        Assert.DoesNotContain("async-secret", string.Join(" ", failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ObserveAsync_TreatsCancellationAsNormalAndContainsErrorReporterFailure()
    {
        var feedbackCount = 0;

        await CopilotUiTaskObserver.ObserveAsync(
            () => Task.FromCanceled(new CancellationToken(canceled: true)),
            "cancelled operation",
            _ => Interlocked.Increment(ref feedbackCount));
        await CopilotUiTaskObserver.ObserveAsync(
            () => Task.FromException(new InvalidOperationException("operation failed")),
            "reporter operation",
            _ => throw new InvalidOperationException("reporter failed"));

        Assert.Equal(0, Volatile.Read(ref feedbackCount));
    }
}
