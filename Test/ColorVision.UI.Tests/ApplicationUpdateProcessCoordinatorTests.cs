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
                    requestClose: _ => SingleInstanceCloseRequestResult.Rejected));

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
