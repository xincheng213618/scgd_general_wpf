using ColorVision.Update;
using ColorVision.UI.Plugins;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace ColorVision.UI.Tests
{
    public sealed class UpdaterBatchExecutionTests : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionUpdateE2E-{Guid.NewGuid():N}");

        public UpdaterBatchExecutionTests()
        {
            Directory.CreateDirectory(_rootDirectory);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public async Task ApplicationBatchCopiesFilesAndCleansSpecialCharacterStageDirectory()
        {
            string tempRoot = Path.Combine(_rootDirectory, "Application Update Root %CACHE%!");
            string stageDirectory = Path.Combine(tempRoot, "ColorVision");
            string targetDirectory = Path.Combine(_rootDirectory, "Application Target %PATH%! & ^");
            Directory.CreateDirectory(stageDirectory);
            Directory.CreateDirectory(Path.Combine(tempRoot, "Packages"));
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(Path.Combine(stageDirectory, "payload.txt"), "new");
            File.WriteAllText(Path.Combine(tempRoot, "Packages", "temporary.txt"), "temporary");
            File.WriteAllText(Path.Combine(targetDirectory, "payload.txt"), "old");

            string executableName = CreateProbeExecutable(stageDirectory);
            string batchPath = Path.Combine(tempRoot, "update.bat");
            File.WriteAllText(
                batchPath,
                BuildApplicationBatch(stageDirectory, tempRoot, targetDirectory, executableName, restartApplication: false),
                new UTF8Encoding(false));

            BatchResult result = await RunBatchAsync(batchPath);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.True(
                string.Equals("new", File.ReadAllText(Path.Combine(targetDirectory, "payload.txt")), StringComparison.Ordinal),
                result.ToString());
            Assert.True(File.Exists(Path.Combine(targetDirectory, executableName)));
            Assert.False(File.Exists(Path.Combine(targetDirectory, "update.bat")));
            Assert.False(Directory.Exists(Path.Combine(targetDirectory, "Packages")));
            Assert.True(await WaitForDirectoryDeletionAsync(tempRoot));
        }

        [Fact]
        public async Task ApplicationBatchCleansUpdateRootWhenCopyFails()
        {
            string tempRoot = Path.Combine(_rootDirectory, "Failed Update Root");
            string stageDirectory = Path.Combine(tempRoot, "ColorVision");
            string invalidTarget = Path.Combine(_rootDirectory, "Target Is A File");
            Directory.CreateDirectory(stageDirectory);
            File.WriteAllText(Path.Combine(stageDirectory, "payload.txt"), "new");
            File.WriteAllText(invalidTarget, "not a directory");

            string batchPath = Path.Combine(tempRoot, "update.bat");
            File.WriteAllText(
                batchPath,
                BuildApplicationBatch(stageDirectory, tempRoot, invalidTarget, "ColorVisionTestProbe.exe", restartApplication: false),
                new UTF8Encoding(false));

            BatchResult result = await RunBatchAsync(batchPath);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal("not a directory", File.ReadAllText(invalidTarget));
            Assert.True(await WaitForDirectoryDeletionAsync(tempRoot));
        }

        [Fact]
        public async Task PluginBatchCopiesPluginAndCleansSpecialCharacterStageDirectory()
        {
            string tempRoot = Path.Combine(_rootDirectory, "Plugin Stage");
            string stageRoot = Path.Combine(tempRoot, "ColorVision");
            string stagedPluginDirectory = Path.Combine(stageRoot, "Plugins", "third.party");
            string targetDirectory = Path.Combine(_rootDirectory, "Plugin Target %PATH%! & ^");
            Directory.CreateDirectory(stagedPluginDirectory);
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(Path.Combine(stagedPluginDirectory, "payload.txt"), "plugin");

            string executableName = CreateProbeExecutable(targetDirectory);
            string batchPath = Path.Combine(tempRoot, "update.bat");
            File.WriteAllText(batchPath, string.Empty);
            ExitUpdateHandoffState handoffState = ExitUpdateHandoff.Prepare(targetDirectory, tempRoot, Path.Combine(_rootDirectory, "PluginState"));
            PluginUpdater.GenerateBatchFile(batchPath, targetDirectory, executableName, int.MaxValue, handoffState, restartArguments: null);

            BatchResult result = await RunBatchAsync(batchPath);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Equal("plugin", File.ReadAllText(Path.Combine(targetDirectory, "Plugins", "third.party", "payload.txt")));
            Assert.True(await WaitForDirectoryDeletionAsync(tempRoot));
        }

        [Fact]
        public void ElevatedApplicationBatchRepairsMissingServiceHostWithoutAclRewrite()
        {
            string batch = BuildApplicationBatch(
                @"C:\Update Root %PATH%! & ^\ColorVision",
                @"C:\Update Root %PATH%! & ^",
                @"D:\ColorVision Portable",
                "ColorVision.exe",
                repairServiceHost: true);

            Assert.Contains("set \"REPAIR_SERVICE_HOST=1\"", batch, StringComparison.Ordinal);
            Assert.Contains(":repair_service_host", batch, StringComparison.Ordinal);
            Assert.Contains("sc.exe create", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sc.exe config", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("%ProgramData%\\ColorVision\\ServiceHost", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("install.log", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("icacls", batch, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExitUpdateBatchDoesNotRestartApplication()
        {
            string batch = BuildApplicationBatch(
                @"C:\Update Root\ColorVision",
                @"C:\Update Root",
                @"D:\ColorVision",
                "ColorVision.exe",
                restartApplication: false);

            Assert.Contains("set \"RESTART_APPLICATION=0\"", batch, StringComparison.Ordinal);
            Assert.Contains("if exist \"%REOPEN_REQUEST%\" start \"\" /b \"%EXEPATH%\"", batch, StringComparison.Ordinal);
            Assert.DoesNotContain("taskkill /f /im", batch, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExitUpdateBatchOnlyTargetsOriginalProcess()
        {
            string batch = BuildApplicationBatch(
                @"C:\Update Root\ColorVision",
                @"C:\Update Root",
                @"D:\ColorVision",
                "ColorVision.exe",
                restartApplication: false,
                originalProcessId: 4242);

            Assert.Contains("set \"ORIGINAL_PID=4242\"", batch, StringComparison.Ordinal);
            Assert.Contains("tasklist /fi \"PID eq %ORIGINAL_PID%\"", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("taskkill /f /pid \"%ORIGINAL_PID%\"", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("taskkill /f /im", batch, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExitUpdateBatchReopensApplicationWhenLaunchWasRequested()
        {
            string tempRoot = Path.Combine(_rootDirectory, "Requested Reopen Root");
            string stageDirectory = Path.Combine(tempRoot, "ColorVision");
            string targetDirectory = Path.Combine(_rootDirectory, "Requested Reopen Target");
            string handoffDirectory = Path.Combine(tempRoot, "handoff");
            string openedMarkerPath = Path.Combine(targetDirectory, "opened.txt");
            Directory.CreateDirectory(stageDirectory);
            Directory.CreateDirectory(targetDirectory);
            Directory.CreateDirectory(handoffDirectory);
            File.WriteAllText(Path.Combine(stageDirectory, "payload.txt"), "updated");

            const string executableName = "reopen.cmd";
            File.WriteAllText(
                Path.Combine(targetDirectory, executableName),
                $"@echo off{Environment.NewLine}>\"{openedMarkerPath}\" echo opened",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(handoffDirectory, "update.pending"), "pending");
            File.WriteAllText(Path.Combine(handoffDirectory, "reopen.requested"), "requested");

            string batchPath = Path.Combine(tempRoot, "update.bat");
            File.WriteAllText(
                batchPath,
                BuildApplicationBatch(stageDirectory, tempRoot, targetDirectory, executableName, restartApplication: false),
                new UTF8Encoding(false));

            BatchResult result = await RunBatchAsync(batchPath);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.True(await WaitForFileAsync(openedMarkerPath), result.ToString());
        }

        [Fact]
        public async Task ServiceHostRepairBatchCopiesPortablePackageAndKeepsApplicationUpdateSuccessful()
        {
            string tempRoot = Path.Combine(_rootDirectory, "Repair Update Root");
            string stageDirectory = Path.Combine(tempRoot, "ColorVision");
            string servicePackageDirectory = Path.Combine(stageDirectory, "ServiceHost");
            string targetDirectory = Path.Combine(_rootDirectory, "Repair Target");
            string fakeProgramData = Path.Combine(_rootDirectory, "ProgramData");
            string fakeTools = Path.Combine(_rootDirectory, "Fake Tools");
            Directory.CreateDirectory(servicePackageDirectory);
            Directory.CreateDirectory(targetDirectory);
            Directory.CreateDirectory(fakeTools);
            File.WriteAllText(Path.Combine(stageDirectory, "payload.txt"), "updated");
            File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), Path.Combine(servicePackageDirectory, "ColorVisionServiceHost.exe"));
            File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), Path.Combine(fakeTools, "sc.exe"));

            string executableName = CreateProbeExecutable(stageDirectory);
            string batchPath = Path.Combine(tempRoot, "update.bat");
            File.WriteAllText(
                batchPath,
                BuildApplicationBatch(stageDirectory, tempRoot, targetDirectory, executableName, repairServiceHost: true),
                new UTF8Encoding(false));

            BatchResult result = await RunBatchAsync(batchPath, new Dictionary<string, string>
            {
                ["ProgramData"] = fakeProgramData,
                ["PATH"] = fakeTools + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            });

            string installedServiceHost = Path.Combine(fakeProgramData, "ColorVision", "ServiceHost", "ColorVisionServiceHost.exe");
            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Equal("updated", File.ReadAllText(Path.Combine(targetDirectory, "payload.txt")));
            Assert.True(File.Exists(installedServiceHost), result.ToString());
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(installedServiceHost)!, "install.log")), result.ToString());
            Assert.False(File.Exists(Path.Combine(targetDirectory, "update.bat")));
            Assert.True(await WaitForDirectoryDeletionAsync(tempRoot));
        }

        private static string BuildApplicationBatch(
            string stageDirectory,
            string cleanupDirectory,
            string targetDirectory,
            string executableName,
            bool repairServiceHost = false,
            bool restartApplication = true,
            int originalProcessId = 999999)
        {
            MethodInfo method = typeof(AutoUpdater).GetMethod(
                "CreateIncrementalUpdateBatch",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(AutoUpdater).FullName, "CreateIncrementalUpdateBatch");

            ExitUpdateHandoffState handoffState = new(
                Path.Combine(cleanupDirectory, "handoff", "update.pending"),
                Path.Combine(cleanupDirectory, "handoff", "reopen.requested"),
                "0123456789abcdef0123456789abcdef",
                cleanupDirectory);
            return (string)method.Invoke(null, [
                stageDirectory,
                cleanupDirectory,
                targetDirectory,
                executableName,
                originalProcessId,
                handoffState,
                repairServiceHost,
                restartApplication])!;
        }

        private static string CreateProbeExecutable(string directory)
        {
            string executableName = $"CVProbe{Guid.NewGuid():N}"[..19] + ".exe";
            File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), Path.Combine(directory, executableName));
            return executableName;
        }

        private static async Task<BatchResult> RunBatchAsync(string batchPath, IReadOnlyDictionary<string, string>? environment = null)
        {
            ProcessStartInfo startInfo = new("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "/d /c call \"%COLORVISION_TEST_BATCH%\"",
            };
            startInfo.Environment["COLORVISION_TEST_BATCH"] = batchPath;
            if (environment != null)
            {
                foreach ((string name, string value) in environment)
                    startInfo.Environment[name] = value;
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start update batch: {batchPath}");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            return new BatchResult(process.ExitCode, await standardOutput, await standardError);
        }

        private static async Task<bool> WaitForDirectoryDeletionAsync(string directory)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                if (!Directory.Exists(directory))
                    return true;
                await Task.Delay(200);
            }

            return !Directory.Exists(directory);
        }

        private static async Task<bool> WaitForFileAsync(string filePath)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                if (File.Exists(filePath))
                    return true;
                await Task.Delay(100);
            }

            return File.Exists(filePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }

        private sealed record BatchResult(int ExitCode, string StandardOutput, string StandardError)
        {
            public override string ToString() => $"Exit code: {ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}";
        }
    }
}
