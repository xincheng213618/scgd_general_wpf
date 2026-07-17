using ColorVisionServiceHost;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostSelfUpdateTests : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"ColorVisionServiceHostUpdate-{Guid.NewGuid():N}");

        public ServiceHostSelfUpdateTests()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        [Fact]
        public void PackageIsCopiedToPrivateStagingDirectory()
        {
            string packageDirectory = Path.Combine(_rootDirectory, "Package");
            string nestedDirectory = Path.Combine(packageDirectory, "Tasks");
            string updateDirectory = Path.Combine(_rootDirectory, "Updates");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(packageDirectory, "ColorVisionServiceHost.exe"), "host");
            File.WriteAllText(Path.Combine(nestedDirectory, "task.ps1"), "task");

            string stagedDirectory = ServiceHostCommandHandler.StageServiceHostPackage(packageDirectory, updateDirectory);

            Assert.StartsWith(Path.Combine(updateDirectory, "Packages"), stagedDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("host", File.ReadAllText(Path.Combine(stagedDirectory, "ColorVisionServiceHost.exe")));
            Assert.Equal("task", File.ReadAllText(Path.Combine(stagedDirectory, "Tasks", "task.ps1")));
        }

        [Fact]
        public void SelfUpdateScriptOwnsAndCleansItsPrivateStage()
        {
            string script = ServiceHostCommandHandler.CreateSelfUpdateScript(
                @"C:\ProgramData\ColorVision\ServiceHost\Updates\Packages\package",
                @"C:\ProgramData\ColorVision\ServiceHost",
                @"C:\ProgramData\ColorVision\ServiceHost\Updates\self-update.ps1",
                @"C:\ProgramData\ColorVision\ServiceHost\Updates\self-update.log");

            Assert.Contains("Start-Sleep -Milliseconds 750", script, StringComparison.Ordinal);
            Assert.Contains("Get-ChildItem -LiteralPath $source -Force | Copy-Item", script, StringComparison.Ordinal);
            Assert.Contains("Remove-Item -LiteralPath $source -Recurse -Force", script, StringComparison.Ordinal);
            Assert.Contains("Remove-Item -LiteralPath $scriptPath -Force", script, StringComparison.Ordinal);
            Assert.Contains("Service host restarted after failed self update.", script, StringComparison.Ordinal);
            Assert.Contains("exit $exitCode", script, StringComparison.Ordinal);
        }

        [Fact]
        public void SelfUpdateScriptIsValidWindowsPowerShell()
        {
            string scriptPath = Path.Combine(_rootDirectory, "self-update.ps1");
            string script = ServiceHostCommandHandler.CreateSelfUpdateScript(
                @"D:\Update Stage\ServiceHost",
                @"C:\ProgramData\ColorVision\ServiceHost",
                scriptPath,
                Path.Combine(_rootDirectory, "self-update.log"));
            File.WriteAllText(scriptPath, script);

            string parserCommand = string.Join(' ',
                "$tokens = $null; $errors = $null;",
                $"[System.Management.Automation.Language.Parser]::ParseFile('{EscapePowerShellLiteral(scriptPath)}', [ref]$tokens, [ref]$errors) | Out-Null;",
                "if ($errors.Count -gt 0) { $errors | ForEach-Object { [Console]::Error.WriteLine($_.Message) }; exit 1 }");
            ProcessStartInfo startInfo = new("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(parserCommand);

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Windows PowerShell.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(30000), "Windows PowerShell parser did not exit within 30 seconds.");
            Assert.True(process.ExitCode == 0, $"Windows PowerShell parser rejected the generated script.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }

        private static string EscapePowerShellLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
