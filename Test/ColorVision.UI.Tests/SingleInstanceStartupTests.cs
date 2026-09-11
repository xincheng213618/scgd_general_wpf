using ColorVision.Update;
using Newtonsoft.Json;
using System.IO;
using System.IO.Pipes;

namespace ColorVision.UI.Tests
{
    public sealed class SingleInstanceStartupTests
    {
        [Fact]
        public void AppConfigDefaultsToSingleInstanceAndIgnoresTheLegacyIsMuteKey()
        {
            Assert.False(new APPConfig().AllowMultipleInstances);

            APPConfig upgraded = JsonConvert.DeserializeObject<APPConfig>("""{"IsMute":true}""")!;
            Assert.False(upgraded.AllowMultipleInstances);

            upgraded.AllowMultipleInstances = true;
            string persisted = JsonConvert.SerializeObject(upgraded);
            Assert.Contains("\"AllowMultipleInstances\":true", persisted);
            Assert.DoesNotContain("\"IsMute\"", persisted);
        }

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
        public async Task ReplacementListenerReturnsTheFinalCloseDecision(
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
                await SingleInstanceReplacementListener.TryRequestShutdownAsync(
                    processId,
                    TimeSpan.FromSeconds(2),
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
        public async Task ReplacementRequestReportsUnavailableWhenNoListenerExists()
        {
            int processId = Random.Shared.Next(100_000_000, 2_000_000_000);

            Assert.Equal(
                SingleInstanceCloseRequestResult.Unavailable,
                await SingleInstanceReplacementListener.TryRequestShutdownAsync(
                    processId,
                    TimeSpan.FromMilliseconds(100),
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
                await SingleInstanceReplacementListener.TryRequestShutdownAsync(
                    processId,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(2));

            Assert.Equal(SingleInstanceCloseRequestResult.Indeterminate, result);
            await server.WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task ReplacementRequestTimesOutWhenConnectedPeerDoesNotRespond()
        {
            int processId = Random.Shared.Next(100_000_000, 2_000_000_000);
            using var pipe = new NamedPipeServerStream(
                SingleInstanceReplacementListener.CreatePipeName(processId),
                PipeDirection.Out, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            Task<SingleInstanceCloseRequestResult> request = SingleInstanceReplacementListener.TryRequestShutdownAsync(
                processId, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100));
            await pipe.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(SingleInstanceCloseRequestResult.TimedOut, await request.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ReplacementRequestAllowsResponseAfterConnectionTimeoutHasElapsed()
        {
            int processId = Random.Shared.Next(100_000_000, 2_000_000_000);
            using var pipe = new NamedPipeServerStream(
                SingleInstanceReplacementListener.CreatePipeName(processId),
                PipeDirection.Out, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            Task<SingleInstanceCloseRequestResult> request = SingleInstanceReplacementListener.TryRequestShutdownAsync(
                processId, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
            await pipe.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(200);
            await pipe.WriteAsync(new byte[] { 0 });

            Assert.Equal(SingleInstanceCloseRequestResult.Rejected, await request.WaitAsync(TimeSpan.FromSeconds(5)));
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
