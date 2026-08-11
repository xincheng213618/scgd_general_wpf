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
}
