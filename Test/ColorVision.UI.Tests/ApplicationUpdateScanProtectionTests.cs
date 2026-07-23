using ColorVisionServiceHost;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;

namespace ColorVision.UI.Tests;

public sealed class ApplicationUpdateScanProtectionTests
{
    [Fact]
    public void BeginAndCompleteAddAndRemoveOnlySessionPaths()
    {
        using TestDirectories directories = new();
        FakeDefenderExclusionManager defender = new();
        ApplicationUpdateScanProtectionService service = new(
            defender,
            directories.StateDirectory,
            () => new DateTimeOffset(2026, 7, 23, 4, 0, 0, TimeSpan.Zero));

        ServiceHostResponse beginResponse = service.Begin(
            CreateRequest("begin-application-update-scan-protection", new
            {
                updateRoot = directories.UpdateRoot,
                lifetimeSeconds = 180,
            }),
            directories.Context);

        Assert.True(beginResponse.Success, beginResponse.Message);
        string protectionId = Assert.IsType<JValue>(beginResponse.Data!["protectionId"]).Value<string>()!;
        Assert.True(Guid.TryParseExact(protectionId, "N", out _));
        Assert.Equal(
            [directories.UpdateRoot, directories.ApplicationDirectory],
            defender.AddedPaths,
            StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(directories.StateDirectory, protectionId + ".json")));

        ServiceHostResponse completeResponse = service.Complete(
            CreateRequest("complete-application-update-scan-protection", new { protectionId }),
            directories.Context);

        Assert.True(completeResponse.Success, completeResponse.Message);
        Assert.Equal(defender.AddedPaths, defender.RemovedPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(directories.StateDirectory, "*.json"));
    }

    [Fact]
    public void ExpiredSessionIsRemovedByCleanup()
    {
        using TestDirectories directories = new();
        FakeDefenderExclusionManager defender = new();
        DateTimeOffset utcNow = new(2026, 7, 23, 4, 0, 0, TimeSpan.Zero);
        ApplicationUpdateScanProtectionService service = new(
            defender,
            directories.StateDirectory,
            () => utcNow);

        ServiceHostResponse beginResponse = service.Begin(
            CreateRequest("begin-application-update-scan-protection", new
            {
                updateRoot = directories.UpdateRoot,
                lifetimeSeconds = 30,
            }),
            directories.Context);
        Assert.True(beginResponse.Success, beginResponse.Message);

        utcNow = utcNow.AddSeconds(31);
        service.CleanupExpiredStatesNow();

        Assert.Equal(defender.AddedPaths, defender.RemovedPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(directories.StateDirectory, "*.json"));
    }

    [Fact]
    public void RecoveryJournalRemovesPathsLeftBeforeStatePersistence()
    {
        using TestDirectories directories = new();
        FakeDefenderExclusionManager defender = new();
        ApplicationUpdateScanProtectionService service = new(defender, directories.StateDirectory);
        string[] paths = [directories.UpdateRoot, directories.ApplicationDirectory];
        string recoveryJournalPath = Path.Combine(directories.StateDirectory, $"{Guid.NewGuid():N}.pending");
        File.WriteAllLines(
            recoveryJournalPath,
            paths.Select(path => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(path))));

        service.CleanupExpiredStatesNow();

        Assert.Equal(paths, defender.RemovedPaths, StringComparer.OrdinalIgnoreCase);
        Assert.False(File.Exists(recoveryJournalPath));
    }

    [Fact]
    public void UpdateRootOutsideSystemTemporaryDirectoryIsRejected()
    {
        using TestDirectories directories = new();
        string invalidUpdateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            $"ColorVisionUpdate-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(invalidUpdateRoot);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ApplicationUpdateScanProtectionService.ResolvePaths(directories.Context, invalidUpdateRoot));

            Assert.Contains("system temporary directory", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(invalidUpdateRoot))
                Directory.Delete(invalidUpdateRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("AddScript")]
    [InlineData("RemoveScript")]
    public void DefenderExclusionScriptIsValidWindowsPowerShell(string fieldName)
    {
        FieldInfo field = typeof(PowerShellDefenderExclusionManager).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Static)!;
        string script = Assert.IsType<string>(field.GetRawConstantValue());
        string encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        string parserCommand = string.Join(' ',
            "$tokens = $null; $errors = $null;",
            "$script = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($env:COLORVISION_TEST_SCRIPT));",
            "[System.Management.Automation.Language.Parser]::ParseInput($script, [ref]$tokens, [ref]$errors) | Out-Null;",
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
        startInfo.Environment["COLORVISION_TEST_SCRIPT"] = encodedScript;

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Windows PowerShell.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30000), "Windows PowerShell parser did not exit within 30 seconds.");
        Assert.True(process.ExitCode == 0, $"Windows PowerShell parser rejected {fieldName}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    private static ServiceHostRequest CreateRequest(string command, object data)
    {
        return new ServiceHostRequest
        {
            Command = command,
            Data = JToken.FromObject(data),
        };
    }

    private sealed class FakeDefenderExclusionManager : IDefenderExclusionManager
    {
        public List<string> AddedPaths { get; } = [];

        public List<string> RemovedPaths { get; } = [];

        public DefenderExclusionChangeResult AddPaths(IReadOnlyCollection<string> paths, string recoveryJournalPath)
        {
            AddedPaths.AddRange(paths);
            return DefenderExclusionChangeResult.Succeeded(paths.ToArray(), []);
        }

        public DefenderExclusionChangeResult RemovePaths(IReadOnlyCollection<string> paths)
        {
            RemovedPaths.AddRange(paths);
            return DefenderExclusionChangeResult.Succeeded(paths.ToArray(), []);
        }
    }

    private sealed class TestDirectories : IDisposable
    {
        private readonly string _root;

        public TestDirectories()
        {
            _root = Path.Combine(Path.GetTempPath(), $"ColorVisionScanProtectionTests-{Guid.NewGuid():N}");
            ApplicationDirectory = Path.Combine(_root, "Application");
            StateDirectory = Path.Combine(_root, "State");
            UpdateRoot = Path.Combine(Path.GetTempPath(), $"ColorVisionUpdate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(ApplicationDirectory);
            Directory.CreateDirectory(StateDirectory);
            Directory.CreateDirectory(UpdateRoot);
            string applicationPath = Path.Combine(ApplicationDirectory, "ColorVision.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), applicationPath);
            Context = new ServiceHostRequestContext
            {
                ProcessPath = applicationPath,
                ProcessId = Environment.ProcessId,
                UserSid = WindowsIdentity.GetCurrent().User!.Value,
                UserName = "test",
            };
        }

        public string ApplicationDirectory { get; }

        public string StateDirectory { get; }

        public string UpdateRoot { get; }

        public ServiceHostRequestContext Context { get; }

        public void Dispose()
        {
            if (Directory.Exists(UpdateRoot))
                Directory.Delete(UpdateRoot, recursive: true);
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
