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
            string stageDirectory = Path.Combine(_rootDirectory, "Application Stage");
            string targetDirectory = Path.Combine(_rootDirectory, "Application Target %PATH%! & ^");
            Directory.CreateDirectory(stageDirectory);
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(Path.Combine(stageDirectory, "payload.txt"), "new");
            File.WriteAllText(Path.Combine(targetDirectory, "payload.txt"), "old");

            string executableName = CreateProbeExecutable(stageDirectory);
            string batchPath = Path.Combine(stageDirectory, "update.bat");
            File.WriteAllText(
                batchPath,
                BuildApplicationBatch(stageDirectory, targetDirectory, executableName, restartApplication: false),
                new UTF8Encoding(false));

            BatchResult result = await RunBatchAsync(batchPath);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.True(
                string.Equals("new", File.ReadAllText(Path.Combine(targetDirectory, "payload.txt")), StringComparison.Ordinal),
                result.ToString());
            Assert.True(File.Exists(Path.Combine(targetDirectory, executableName)));
            Assert.True(await WaitForDirectoryDeletionAsync(stageDirectory));
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
            PluginUpdater.GenerateBatchFile(batchPath, targetDirectory, executableName, restartArguments: null);

            BatchResult result = await RunBatchAsync(batchPath);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Equal("plugin", File.ReadAllText(Path.Combine(targetDirectory, "Plugins", "third.party", "payload.txt")));
            Assert.True(await WaitForDirectoryDeletionAsync(tempRoot));
        }

        [Fact]
        public void ElevatedApplicationBatchRepairsMissingServiceHostWithoutAclRewrite()
        {
            string batch = BuildApplicationBatch(
                @"C:\Update Stage %PATH%! & ^",
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
                @"C:\Update Stage",
                @"D:\ColorVision",
                "ColorVision.exe",
                restartApplication: false);

            Assert.Contains("set \"RESTART_APPLICATION=0\"", batch, StringComparison.Ordinal);
            Assert.Contains("if \"%RESTART_APPLICATION%\"==\"1\" start \"\" /b \"%EXEPATH%\"", batch, StringComparison.Ordinal);
            Assert.DoesNotContain("\r\nstart \"\" /b \"%EXEPATH%\"", batch, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ServiceHostRepairBatchCopiesPortablePackageAndKeepsApplicationUpdateSuccessful()
        {
            string stageDirectory = Path.Combine(_rootDirectory, "Repair Stage");
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
            string batchPath = Path.Combine(stageDirectory, "update.bat");
            File.WriteAllText(
                batchPath,
                BuildApplicationBatch(stageDirectory, targetDirectory, executableName, repairServiceHost: true),
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
            Assert.True(await WaitForDirectoryDeletionAsync(stageDirectory));
        }

        private static string BuildApplicationBatch(
            string stageDirectory,
            string targetDirectory,
            string executableName,
            bool repairServiceHost = false,
            bool restartApplication = true)
        {
            MethodInfo method = typeof(AutoUpdater).GetMethod(
                "CreateIncrementalUpdateBatch",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(AutoUpdater).FullName, "CreateIncrementalUpdateBatch");

            return (string)method.Invoke(null, [stageDirectory, targetDirectory, executableName, repairServiceHost, restartApplication])!;
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
