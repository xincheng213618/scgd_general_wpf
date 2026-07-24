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
                gracefulShutdownTimeout: TimeSpan.FromMilliseconds(100),
                forcedShutdownTimeout: TimeSpan.FromSeconds(5));

            Assert.Equal(2, closedCount);
            Assert.True(processA1.WaitForExit(5000));
            Assert.True(processA2.WaitForExit(5000));
            Assert.False(processB.HasExited);
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
