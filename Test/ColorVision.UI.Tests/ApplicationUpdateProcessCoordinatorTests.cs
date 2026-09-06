using ColorVision.Update;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class ApplicationUpdateProcessCoordinatorTests : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionProcessCoordinator-{Guid.NewGuid():N}");
        private readonly List<Process> _processes = new();

        public ApplicationUpdateProcessCoordinatorTests()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        [Fact]
        public void ClosesAllProcessesFromCurrentInstallationWithoutTouchingAnotherCopy()
        {
            string installationA = Path.Combine(_rootDirectory, "InstallationA");
            string installationB = Path.Combine(_rootDirectory, "InstallationB");
            Directory.CreateDirectory(installationA);
            Directory.CreateDirectory(installationB);

            const string executableName = "ColorVisionProcessProbe.exe";
            string executableA = Path.Combine(installationA, executableName);
            string executableB = Path.Combine(installationB, executableName);
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executableA);
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executableB);

            Process processA1 = StartProbe(executableA);
            Process processA2 = StartProbe(executableA);
            Process processB = StartProbe(executableB);
            Assert.True(SpinWait.SpinUntil(
                () => !processA1.HasExited && !processA2.HasExited && !processB.HasExited,
                TimeSpan.FromSeconds(2)));

            int closedCount = ApplicationUpdateProcessCoordinator.CloseOtherApplicationProcesses(
                executableA,
                currentProcessId: -1,
                forcedShutdownTimeout: TimeSpan.FromSeconds(5));

            Assert.Equal(2, closedCount);
            Assert.True(processA1.WaitForExit(5000));
            Assert.True(processA2.WaitForExit(5000));
            Assert.False(processB.HasExited);
        }

        [Fact]
        public void KeepsCurrentProcessAndClosesEarlierProcessFromTheSameInstallation()
        {
            string installationA = Path.Combine(_rootDirectory, "InstallationA");
            string installationB = Path.Combine(_rootDirectory, "InstallationB");
            Directory.CreateDirectory(installationA);
            Directory.CreateDirectory(installationB);

            const string executableName = "ColorVisionProcessProbe.exe";
            string executableA = Path.Combine(installationA, executableName);
            string executableB = Path.Combine(installationB, executableName);
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executableA);
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executableB);

            Process earlierProcess = StartProbe(executableA);
            Process currentProcess = StartProbe(executableA);
            Process otherInstallationProcess = StartProbe(executableB);
            Assert.True(SpinWait.SpinUntil(
                () => !earlierProcess.HasExited && !currentProcess.HasExited && !otherInstallationProcess.HasExited,
                TimeSpan.FromSeconds(2)));

            int closedCount = ApplicationUpdateProcessCoordinator.CloseOtherApplicationProcesses(
                executableA,
                currentProcess.Id,
                forcedShutdownTimeout: TimeSpan.FromSeconds(5));

            Assert.Equal(1, closedCount);
            Assert.True(earlierProcess.WaitForExit(5000));
            Assert.False(currentProcess.HasExited);
            Assert.False(otherInstallationProcess.HasExited);
        }

        [Fact]
        public void StartupReplacementTargetsOnlyEarlierProcessesFromTheSameInstallationAndSession()
        {
            string installationA = Path.Combine(_rootDirectory, "InstallationA");
            string installationB = Path.Combine(_rootDirectory, "InstallationB");
            Directory.CreateDirectory(installationA);
            Directory.CreateDirectory(installationB);

            string executablePath = Path.Combine(installationA, "ColorVisionProcessProbe.exe");
            string otherExecutablePath = Path.Combine(installationB, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), otherExecutablePath);

            Process firstEarlierProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process secondEarlierProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process otherInstallationProcess = StartProbe(otherExecutablePath);
            Thread.Sleep(100);
            Process currentProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process laterProcess = StartProbe(executablePath);
            Assert.True(SpinWait.SpinUntil(
                () => !firstEarlierProcess.HasExited
                    && !secondEarlierProcess.HasExited
                    && !otherInstallationProcess.HasExited
                    && !currentProcess.HasExited
                    && !laterProcess.HasExited,
                TimeSpan.FromSeconds(2)));
            Assert.True(firstEarlierProcess.StartTime < secondEarlierProcess.StartTime);
            Assert.True(secondEarlierProcess.StartTime < currentProcess.StartTime);
            Assert.True(currentProcess.StartTime < laterProcess.StartTime);

            Assert.Equal(0, ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                executablePath,
                currentProcess.Id,
                currentProcess.SessionId + 1,
                currentProcess.StartTime.ToUniversalTime(),
                gracefulShutdownTimeout: TimeSpan.Zero,
                requestClose: _ => throw new InvalidOperationException("No process should match another session.")));

            var requestedProcessIds = new List<int>();
            int closedCount = ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                executablePath,
                currentProcess.Id,
                currentProcess.SessionId,
                currentProcess.StartTime.ToUniversalTime(),
                gracefulShutdownTimeout: TimeSpan.FromMilliseconds(100),
                requestClose: processId =>
                {
                    Assert.True(ApplicationUpdateProcessCoordinator.IsSingleInstanceReplacementRequested(processId));
                    if (requestedProcessIds.Count > 0)
                    {
                        Process previousProcess = _processes.Single(item => item.Id == requestedProcessIds[^1]);
                        Assert.True(previousProcess.HasExited);
                    }
                    requestedProcessIds.Add(processId);
                    Process process = _processes.Single(item => item.Id == processId);
                    process.Kill(entireProcessTree: true);
                    return SingleInstanceCloseRequestResult.Accepted;
                });

            Assert.Equal(2, closedCount);
            Assert.Equal([firstEarlierProcess.Id, secondEarlierProcess.Id], requestedProcessIds);
            Assert.True(firstEarlierProcess.WaitForExit(5000));
            Assert.True(secondEarlierProcess.WaitForExit(5000));
            Assert.False(currentProcess.HasExited);
            Assert.False(laterProcess.HasExited);
            Assert.False(otherInstallationProcess.HasExited);
        }

        [Fact]
        public void StartupReplacementRejectionPreservesEarlierProcess()
        {
            string installationPath = Path.Combine(_rootDirectory, "InstallationA");
            Directory.CreateDirectory(installationPath);

            string executablePath = Path.Combine(installationPath, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);

            Process earlierProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process currentProcess = StartProbe(executablePath);
            Assert.True(SpinWait.SpinUntil(
                () => !earlierProcess.HasExited && !currentProcess.HasExited,
                TimeSpan.FromSeconds(2)));
            Assert.True(earlierProcess.StartTime < currentProcess.StartTime);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                    executablePath,
                    currentProcess.Id,
                    currentProcess.SessionId,
                    currentProcess.StartTime.ToUniversalTime(),
                    gracefulShutdownTimeout: TimeSpan.Zero,
                    requestClose: _ => SingleInstanceCloseRequestResult.Rejected,
                    confirmTermination: _ => throw new InvalidOperationException("A declined close must not offer termination.")));

            Assert.Contains("declined", exception.Message);
            Assert.False(earlierProcess.HasExited);
            Assert.False(currentProcess.HasExited);
        }

        [Fact]
        public void StartupReplacementAcceptedButNotExitedTimesOutWithoutKilling()
        {
            string installationPath = Path.Combine(_rootDirectory, "InstallationA");
            Directory.CreateDirectory(installationPath);

            string executablePath = Path.Combine(installationPath, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);

            Process earlierProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process currentProcess = StartProbe(executablePath);
            Assert.True(SpinWait.SpinUntil(
                () => !earlierProcess.HasExited && !currentProcess.HasExited,
                TimeSpan.FromSeconds(2)));
            Assert.True(earlierProcess.StartTime < currentProcess.StartTime);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                    executablePath,
                    currentProcess.Id,
                    currentProcess.SessionId,
                    currentProcess.StartTime.ToUniversalTime(),
                    gracefulShutdownTimeout: TimeSpan.FromMilliseconds(100),
                    requestClose: _ => SingleInstanceCloseRequestResult.Accepted));

            Assert.Contains("did not exit", exception.Message);
            Assert.False(earlierProcess.HasExited);
            Assert.False(currentProcess.HasExited);
        }

        [Fact]
        public void StartupReplacementWithoutListenerOrWindowPreservesEarlierProcess()
        {
            string installationPath = Path.Combine(_rootDirectory, "InstallationA");
            Directory.CreateDirectory(installationPath);

            string executablePath = Path.Combine(installationPath, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);

            Process earlierProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process currentProcess = StartProbe(executablePath);
            Assert.True(earlierProcess.StartTime < currentProcess.StartTime);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                    executablePath,
                    currentProcess.Id,
                    currentProcess.SessionId,
                    currentProcess.StartTime.ToUniversalTime(),
                    gracefulShutdownTimeout: TimeSpan.Zero,
                    requestClose: _ => SingleInstanceCloseRequestResult.Unavailable));

            Assert.Contains("no safe close endpoint", exception.Message);
            Assert.False(earlierProcess.HasExited);
            Assert.False(currentProcess.HasExited);
        }

        [Theory]
        [InlineData(SingleInstanceCloseRequestResult.Unavailable)]
        [InlineData(SingleInstanceCloseRequestResult.TimedOut)]
        [InlineData(SingleInstanceCloseRequestResult.Accepted)]
        public void ConfirmedRecoveryOnlyTerminatesEarlierProcessFromSameInstallation(SingleInstanceCloseRequestResult response)
        {
            string installationA = Path.Combine(_rootDirectory, "InstallationA");
            string installationB = Path.Combine(_rootDirectory, "InstallationB");
            Directory.CreateDirectory(installationA);
            Directory.CreateDirectory(installationB);
            string executableA = Path.Combine(installationA, "ColorVisionProcessProbe.exe");
            string executableB = Path.Combine(installationB, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executableA);
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executableB);
            Process earlierProcess = StartProbe(executableA);
            Process otherInstallationProcess = StartProbe(executableB);
            Thread.Sleep(100);
            Process currentProcess = StartProbe(executableA);
            Thread.Sleep(100);
            Process laterProcess = StartProbe(executableA);
            int confirmations = 0;

            int closed = ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                executableA, currentProcess.Id, currentProcess.SessionId, currentProcess.StartTime.ToUniversalTime(),
                gracefulShutdownTimeout: TimeSpan.Zero, requestClose: _ => response,
                confirmTermination: processId =>
                {
                    confirmations++;
                    Assert.Equal(earlierProcess.Id, processId);
                    Assert.False(earlierProcess.HasExited);
                    return true;
                });

            Assert.Equal(1, closed);
            Assert.Equal(1, confirmations);
            Assert.True(earlierProcess.WaitForExit(5000));
            Assert.False(currentProcess.HasExited);
            Assert.False(laterProcess.HasExited);
            Assert.False(otherInstallationProcess.HasExited);
        }

        [Fact]
        public void DecliningRecoveryPreservesEarlierProcess()
        {
            string executablePath = Path.Combine(_rootDirectory, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);
            Process earlierProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process currentProcess = StartProbe(executablePath);

            Assert.Throws<OperationCanceledException>(() => ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                executablePath, currentProcess.Id, currentProcess.SessionId, currentProcess.StartTime.ToUniversalTime(),
                gracefulShutdownTimeout: TimeSpan.Zero, requestClose: _ => SingleInstanceCloseRequestResult.TimedOut,
                confirmTermination: _ => false));

            Assert.False(earlierProcess.HasExited);
            Assert.False(currentProcess.HasExited);
        }

        [Fact]
        public void RecoveryHandlesProcessExitingDuringConfirmation()
        {
            string executablePath = Path.Combine(_rootDirectory, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);
            Process earlierProcess = StartProbe(executablePath);
            Thread.Sleep(100);
            Process currentProcess = StartProbe(executablePath);

            int closed = ApplicationUpdateProcessCoordinator.CloseEarlierApplicationProcesses(
                executablePath, currentProcess.Id, currentProcess.SessionId, currentProcess.StartTime.ToUniversalTime(),
                gracefulShutdownTimeout: TimeSpan.Zero, requestClose: _ => SingleInstanceCloseRequestResult.TimedOut,
                confirmTermination: _ =>
                {
                    earlierProcess.Kill();
                    Assert.True(earlierProcess.WaitForExit(5000));
                    return true;
                });

            Assert.Equal(1, closed);
            Assert.False(currentProcess.HasExited);
        }

        [Fact]
        public async Task InteractiveStartupForcesWindowlessOlderProcessesAndPreservesOtherInstances()
        {
            string executablePath = Path.Combine(_rootDirectory, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);
            string otherDirectory = Path.Combine(_rootDirectory, "Other");
            Directory.CreateDirectory(otherDirectory);
            string otherExecutable = Path.Combine(otherDirectory, "ColorVisionProcessProbe.exe");
            File.Copy(executablePath, otherExecutable);

            Process old = StartProbe(executablePath);
            Process other = StartProbe(otherExecutable);
            Thread.Sleep(100);
            Process current = StartProbe(executablePath);
            Thread.Sleep(100);
            Process newer = StartProbe(executablePath);
            Assert.Equal(IntPtr.Zero, old.MainWindowHandle);

            using var differentSession = ApplicationUpdateProcessCoordinator.PrepareStartupReplacement(
                executablePath, current.Id, current.SessionId + 1, current.StartTime.ToUniversalTime());
            Assert.Empty(differentSession.ProcessIds);
            using var replacement = ApplicationUpdateProcessCoordinator.PrepareStartupReplacement(
                executablePath, current.Id, current.SessionId, current.StartTime.ToUniversalTime());
            Assert.Equal(old.Id, Assert.Single(replacement.ProcessIds));

            await replacement.ForceCloseAsync(new Progress<string>(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(old.HasExited);
            Assert.False(current.HasExited);
            Assert.False(newer.HasExited);
            Assert.False(other.HasExited);
        }

        [Fact]
        public async Task InteractiveStartupCancellationPreservesProcessesNotYetTerminated()
        {
            string executablePath = Path.Combine(_rootDirectory, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);
            Process old = StartProbe(executablePath);
            using var replacement = ApplicationUpdateProcessCoordinator.PrepareStartupReplacement(
                executablePath, -1, old.SessionId, old.StartTime.ToUniversalTime().AddSeconds(1));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => replacement.ForceCloseAsync(new Progress<string>(), cancellation.Token));
            Assert.False(old.HasExited);
        }

        [Fact]
        public async Task TerminationWaitReportsTimeoutWhileTheProcessStillExists()
        {
            string executablePath = Path.Combine(_rootDirectory, "ColorVisionProcessProbe.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), executablePath);
            Process old = StartProbe(executablePath);

            TimeoutException error = await Assert.ThrowsAsync<TimeoutException>(() =>
                ApplicationUpdateProcessCoordinator.WaitForForcedExitAsync(old, TimeSpan.FromMilliseconds(100), CancellationToken.None));

            Assert.Contains(old.Id.ToString(), error.Message);
            Assert.False(old.HasExited);
            using var cancellation = new CancellationTokenSource();
            Task wait = ApplicationUpdateProcessCoordinator.WaitForForcedExitAsync(old, TimeSpan.FromSeconds(5), cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
            Assert.False(old.HasExited);
        }

        private Process StartProbe(string executablePath)
        {
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "-t 127.0.0.1",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            }) ?? throw new InvalidOperationException($"Failed to start process probe: {executablePath}");
            _processes.Add(process);
            return process;
        }

        public void Dispose()
        {
            foreach (Process process in _processes)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (Directory.Exists(_rootDirectory))
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    try
                    {
                        Directory.Delete(_rootDirectory, recursive: true);
                        break;
                    }
                    catch (IOException) when (attempt < 19)
                    {
                        Thread.Sleep(100);
                    }
                    catch (UnauthorizedAccessException) when (attempt < 19)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }
    }
}
