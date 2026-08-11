namespace ColorVision.UI.Tests;

public sealed class ApplicationExitCleanupTests
{
    [Fact]
    public void FailedStepDoesNotPreventRemainingCleanup()
    {
        var executed = new List<string>();
        var failures = new List<(string Step, Exception Exception)>();

        ApplicationExitCleanup.Run(
            [
                new("first", () => executed.Add("first")),
                new("failing", () => throw new InvalidOperationException("simulated failure")),
                new("last", () => executed.Add("last"))
            ],
            (step, exception) => failures.Add((step, exception)));

        Assert.Equal(["first", "last"], executed);
        (string step, Exception exception) = Assert.Single(failures);
        Assert.Equal("failing", step);
        Assert.Contains("simulated failure", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureReporterCannotInterruptRemainingCleanup()
    {
        bool lastStepRan = false;

        ApplicationExitCleanup.Run(
            [
                new("failing", () => throw new InvalidOperationException("simulated failure")),
                new("last", () => lastStepRan = true)
            ],
            (_, _) => throw new InvalidOperationException("simulated logger failure"));

        Assert.True(lastStepRan);
    }

    [Fact]
    public void SocketRunsBeforeEligiblePrefetchedUpdate()
    {
        var executed = new List<string>();

        ApplicationExitHandoffState? state = ApplicationExitCleanup.RunSocketBeforePrefetchedUpdate(
            isSessionEnding: false,
            () =>
            {
                executed.Add("resolve");
                return new ApplicationExitHandoffState(false, false);
            },
            () =>
            {
                executed.Add("socket");
                return true;
            },
            () => executed.Add("update"),
            (_, _) => { });

        Assert.Equal(["resolve", "socket", "update"], executed);
        Assert.Equal(new ApplicationExitHandoffState(false, false), state);
    }

    [Fact]
    public void SocketTimeoutDoesNotPreventEligiblePrefetchedUpdate()
    {
        var executed = new List<string>();

        _ = ApplicationExitCleanup.RunSocketBeforePrefetchedUpdate(
            isSessionEnding: false,
            () => new ApplicationExitHandoffState(false, false),
            () =>
            {
                executed.Add("socket-timeout");
                return false;
            },
            () => executed.Add("update"),
            (_, _) => { });

        Assert.Equal(["socket-timeout", "update"], executed);
    }

    [Fact]
    public void SocketFailureDoesNotPreventEligiblePrefetchedUpdate()
    {
        var executed = new List<string>();
        var failures = new List<(string Step, Exception Exception)>();

        _ = ApplicationExitCleanup.RunSocketBeforePrefetchedUpdate(
            isSessionEnding: false,
            () => new ApplicationExitHandoffState(false, false),
            () =>
            {
                executed.Add("socket");
                throw new InvalidOperationException("simulated socket shutdown failure");
            },
            () => executed.Add("update"),
            (step, exception) => failures.Add((step, exception)));

        Assert.Equal(["socket", "update"], executed);
        (string step, Exception exception) = Assert.Single(failures);
        Assert.Equal("socket server", step);
        Assert.Contains("socket shutdown failure", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HandoffResolutionFailureStillStopsSocketAndSkipsPrefetchedUpdate()
    {
        var executed = new List<string>();
        var failures = new List<(string Step, Exception Exception)>();

        ApplicationExitHandoffState? state = ApplicationExitCleanup.RunSocketBeforePrefetchedUpdate(
            isSessionEnding: false,
            () =>
            {
                executed.Add("read-update-state");
                executed.Add("read-replacement-state");
                throw new InvalidOperationException("simulated handoff read failure");
            },
            () =>
            {
                executed.Add("socket");
                return true;
            },
            () => executed.Add("update"),
            (step, exception) => failures.Add((step, exception)));

        Assert.Null(state);
        Assert.Equal(["read-update-state", "read-replacement-state", "socket"], executed);
        (string step, Exception exception) = Assert.Single(failures);
        Assert.Equal("update handoff state", step);
        Assert.Contains("handoff read failure", exception.Message, StringComparison.Ordinal);
    }
}
