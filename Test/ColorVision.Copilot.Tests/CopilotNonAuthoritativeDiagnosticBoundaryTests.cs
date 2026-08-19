using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotNonAuthoritativeDiagnosticBoundaryTests
{
    [Fact]
    public void DiagnosticSinkFailureIsContainedAndCounted()
    {
        var before =
            CopilotNonAuthoritativeDiagnosticBoundary.ContainedFailureCount;

        var written = CopilotNonAuthoritativeDiagnosticBoundary.TryWrite(
            () => throw new InvalidOperationException("sink failed"));

        Assert.False(written);
        Assert.Equal(
            before + 1,
            CopilotNonAuthoritativeDiagnosticBoundary.ContainedFailureCount);
    }

    [Fact]
    public void FatalRuntimeFailuresAreNotReclassifiedAsDiagnostics()
    {
        Assert.Throws<OutOfMemoryException>(() =>
            CopilotNonAuthoritativeDiagnosticBoundary.TryWrite(
                () => throw new OutOfMemoryException()));
    }
}
