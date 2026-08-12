using ColorVision.UI.Desktop.Operations;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsApplicationFailureWatchdogTests
    {
        [Fact]
        public void ProtocolAcceptsOnlyTheFixedProcessArgument()
        {
            Assert.True(OperationsFailureWatchdogProtocol.TryParseTargetProcessId(
                [OperationsFailureWatchdogProtocol.WatchProcessArgument, "123"], out int processId));
            Assert.Equal(123, processId);
            Assert.False(OperationsFailureWatchdogProtocol.TryParseTargetProcessId(
                [OperationsFailureWatchdogProtocol.WatchProcessArgument, "123", "extra"], out _));
            Assert.False(OperationsFailureWatchdogProtocol.TryParseTargetProcessId(
                ["--path", "C:\\Other.exe"], out _));
        }

        [Fact]
        public void TargetIsAlwaysTheSiblingColorVisionExecutable()
        {
            string applicationDirectory = Path.Combine(Path.GetTempPath(), "ColorVision", "bin");
            string watchdogDirectory = Path.Combine(
                applicationDirectory, OperationsFailureWatchdogProtocol.WatchdogDirectoryName);

            Assert.Equal(
                Path.Combine(applicationDirectory, OperationsFailureWatchdogProtocol.TargetExecutableName),
                OperationsFailureWatchdogProtocol.ResolveTargetExecutablePath(watchdogDirectory));
            Assert.True(OperationsFailureWatchdogProtocol.IsExpectedTargetExecutable(
                watchdogDirectory,
                Path.Combine(applicationDirectory, OperationsFailureWatchdogProtocol.TargetExecutableName)));
            Assert.False(OperationsFailureWatchdogProtocol.IsExpectedTargetExecutable(
                watchdogDirectory,
                Path.Combine(applicationDirectory, "Other.exe")));
            Assert.Throws<InvalidOperationException>(() =>
                OperationsFailureWatchdogProtocol.ResolveTargetExecutablePath(
                    Path.Combine(applicationDirectory, "OtherDirectory")));
        }

        [Fact]
        public void RestartRequiresAnUnexpectedExitAfterHealthyLifetime()
        {
            DateTimeOffset startedAt = new(2026, 8, 12, 8, 0, 0, TimeSpan.Zero);

            Assert.False(OperationsFailureWatchdogProtocol.ShouldRestart(
                startedAt, startedAt.AddSeconds(59), cleanExitSignaled: false));
            Assert.True(OperationsFailureWatchdogProtocol.ShouldRestart(
                startedAt, startedAt.AddSeconds(60), cleanExitSignaled: false));
            Assert.False(OperationsFailureWatchdogProtocol.ShouldRestart(
                startedAt, startedAt.AddMinutes(10), cleanExitSignaled: true));
        }
    }
}
