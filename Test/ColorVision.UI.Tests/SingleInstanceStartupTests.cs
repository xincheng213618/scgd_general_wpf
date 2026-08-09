using ColorVision.Update;
using System.IO;
using System.IO.Pipes;

namespace ColorVision.UI.Tests
{
    public sealed class SingleInstanceStartupTests
    {
        [Theory]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, true, false)]
        public void StartupPolicy_ReplacesEarlierInstancesOnlyWhenMultipleInstancesAreDisabled(
            bool isDebuggerAttached,
            bool allowMultipleInstances,
            bool shouldReplaceEarlierInstances)
        {
            SingleInstanceStartupAction expectedAction = shouldReplaceEarlierInstances
                ? SingleInstanceStartupAction.ReplaceEarlierInstances
                : SingleInstanceStartupAction.StartCurrentInstance;

            Assert.Equal(
                expectedAction,
                SingleInstanceStartupPolicy.Decide(isDebuggerAttached, allowMultipleInstances));
        }

        [Theory]
        [InlineData(true, SingleInstanceCloseRequestResult.Accepted)]
        [InlineData(false, SingleInstanceCloseRequestResult.Rejected)]
        public void ReplacementListenerReturnsTheFinalCloseDecision(
            bool closeAccepted,
            SingleInstanceCloseRequestResult expectedResult)
        {
            int processId = Random.Shared.Next(100_000_000, 2_000_000_000);
            int closeCount = 0;
            int finalizeCount = 0;
            using var listener = new SingleInstanceReplacementListener(
                processId,
                () =>
                {
                    Interlocked.Increment(ref closeCount);
                    return closeAccepted;
                },
                () => Interlocked.Increment(ref finalizeCount));

            SingleInstanceCloseRequestResult result =
                SingleInstanceReplacementListener.TryRequestShutdown(
                    processId,
                    TimeSpan.FromSeconds(2));

            Assert.Equal(expectedResult, result);
            Assert.Equal(1, Volatile.Read(ref closeCount));
            if (closeAccepted)
            {
                Assert.True(SpinWait.SpinUntil(
                    () => Volatile.Read(ref finalizeCount) == 1,
                    TimeSpan.FromSeconds(2)));
            }
            else
            {
                Assert.Equal(0, Volatile.Read(ref finalizeCount));
            }
        }

        [Fact]
        public void ReplacementRequestReportsUnavailableWhenNoListenerExists()
        {
            int processId = Random.Shared.Next(100_000_000, 2_000_000_000);

            Assert.Equal(
                SingleInstanceCloseRequestResult.Unavailable,
                SingleInstanceReplacementListener.TryRequestShutdown(
                    processId,
                    TimeSpan.FromMilliseconds(100)));
        }

        [Fact]
        public async Task ReplacementRequestReportsIndeterminateWhenResponseIsLost()
        {
            int processId = Random.Shared.Next(100_000_000, 2_000_000_000);
            using var pipe = new NamedPipeServerStream(
                SingleInstanceReplacementListener.CreatePipeName(processId),
                PipeDirection.Out,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            Task server = Task.Run(async () =>
            {
                await pipe.WaitForConnectionAsync();
                pipe.Disconnect();
            });

            SingleInstanceCloseRequestResult result =
                SingleInstanceReplacementListener.TryRequestShutdown(
                    processId,
                    TimeSpan.FromSeconds(2));

            Assert.Equal(SingleInstanceCloseRequestResult.Indeterminate, result);
            await server.WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void MutexName_IsStableForTheSameExecutablePathAndScopedPerInstallation()
        {
            string installationA = Path.Combine(Path.GetTempPath(), "ColorVision", "InstallationA", "ColorVision.exe");
            string installationB = Path.Combine(Path.GetTempPath(), "ColorVision", "InstallationB", "ColorVision.exe");

            string nameA = SingleInstanceMutexName.Create(installationA);

            Assert.Equal(nameA, SingleInstanceMutexName.Create(installationA.ToUpperInvariant()));
            Assert.NotEqual(nameA, SingleInstanceMutexName.Create(installationB));
        }
    }
}
