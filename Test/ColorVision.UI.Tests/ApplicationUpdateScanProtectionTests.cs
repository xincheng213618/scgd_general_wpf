using ColorVisionServiceHost;
using Newtonsoft.Json;
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
    public async Task StartReturnsTrackedCleanupAndStopWaitsForBlockedInitialCleanup()
    {
        using TestDirectories directories = new();
        BlockingDefenderExclusionManager defender = new();
        ControllableTimeProvider timeProvider = new();
        DateTimeOffset utcNow = new(2026, 8, 12, 4, 0, 0, TimeSpan.Zero);
        WriteExpiredState(directories, utcNow, "initial-cleanup-path");
        ApplicationUpdateScanProtectionService service = new(
            defender,
            directories.StateDirectory,
            () => utcNow,
            timeProvider);

        Task<Task> startInvocation = Task.Factory.StartNew(
            service.Start,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Task? initialCleanupTask = null;
        Task? stopTask = null;
        try
        {
            initialCleanupTask = await startInvocation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Same(initialCleanupTask, service.Start());
            await defender.FirstRemovalStarted.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(initialCleanupTask.IsCompleted);
            Assert.Equal(TimeSpan.FromSeconds(15), timeProvider.Timer.DueTime);
            Assert.Equal(TimeSpan.FromSeconds(15), timeProvider.Timer.Period);

            stopTask = service.StopAsync();
            Assert.Same(stopTask, service.StopAsync());
            Assert.False(stopTask.IsCompleted);
            Assert.Throws<InvalidOperationException>(service.Dispose);

            defender.ReleaseRemoval();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(initialCleanupTask.IsCompletedSuccessfully);
            Assert.Empty(Directory.EnumerateFiles(directories.StateDirectory, "*.json"));
            service.Dispose();
            service.Dispose();
        }
        finally
        {
            defender.ReleaseRemoval();
            initialCleanupTask ??= await startInvocation.WaitAsync(TimeSpan.FromSeconds(5));
            stopTask ??= service.StopAsync();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
            service.Dispose();
        }
    }

    [Fact]
    public async Task StopBeforeStartIsIdempotentAndEnablesConstantTimeDispose()
    {
        using TestDirectories directories = new();
        ControllableTimeProvider timeProvider = new();
        ApplicationUpdateScanProtectionService service = new(
            new FakeDefenderExclusionManager(),
            directories.StateDirectory,
            timeProvider: timeProvider);

        Assert.Throws<InvalidOperationException>(service.Dispose);
        Task stopTask = service.StopAsync();
        Assert.Same(stopTask, service.StopAsync());
        await stopTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, timeProvider.CreateTimerCallCount);
        service.Dispose();
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = service.Start();
        });
    }

    [Fact]
    public async Task StopWaitsForTimerCleanupAndClosedAdmissionPreventsFurtherCleanup()
    {
        using TestDirectories directories = new();
        BlockingDefenderExclusionManager defender = new();
        ControllableTimeProvider timeProvider = new();
        ApplicationUpdateScanProtectionService service = new(
            defender,
            directories.StateDirectory,
            timeProvider: timeProvider);

        await service.Start().WaitAsync(TimeSpan.FromSeconds(5));
        WriteRecoveryJournal(directories.StateDirectory, "first.pending", "first-path");
        WriteRecoveryJournal(directories.StateDirectory, "second.pending", "second-path");

        timeProvider.Timer.Fire();
        Task? stopTask = null;
        try
        {
            await defender.FirstRemovalStarted.WaitAsync(TimeSpan.FromSeconds(5));
            timeProvider.Timer.Fire();
            timeProvider.Timer.Fire();
            Assert.Equal(1, defender.RemoveCallCount);

            stopTask = service.StopAsync();
            Assert.False(stopTask.IsCompleted);
            timeProvider.Timer.Fire();
            Assert.Equal(1, defender.RemoveCallCount);

            defender.ReleaseRemoval();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, defender.RemoveCallCount);
            Assert.Equal(1, timeProvider.Timer.DisposeAsyncCallCount);
            Assert.Single(Directory.EnumerateFiles(directories.StateDirectory, "*.pending"));

            timeProvider.Timer.Fire();
            Assert.Equal(1, defender.RemoveCallCount);
            service.Dispose();
        }
        finally
        {
            defender.ReleaseRemoval();
            stopTask ??= service.StopAsync();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
            service.Dispose();
        }
    }

    [Fact]
    public async Task TimerDisposeFailureIsReportedOnlyAfterActiveCleanupDrains()
    {
        using TestDirectories directories = new();
        BlockingDefenderExclusionManager defender = new();
        ControllableTimeProvider timeProvider = new();
        ApplicationUpdateScanProtectionService service = new(
            defender,
            directories.StateDirectory,
            timeProvider: timeProvider);

        await service.Start().WaitAsync(TimeSpan.FromSeconds(5));
        WriteRecoveryJournal(directories.StateDirectory, "blocked.pending", "blocked-path");
        timeProvider.Timer.Fire();
        Task? stopTask = null;
        try
        {
            await defender.FirstRemovalStarted.WaitAsync(TimeSpan.FromSeconds(5));
            InvalidOperationException marker = new("timer disposal failed");
            timeProvider.Timer.DisposeAsyncFailure = marker;

            stopTask = service.StopAsync();
            Assert.False(stopTask.IsCompleted);

            defender.ReleaseRemoval();
            InvalidOperationException observed = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await stopTask.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Same(marker, observed);

            service.Dispose();
            Assert.Same(stopTask, service.StopAsync());
            int removeCallsAfterStop = defender.RemoveCallCount;
            timeProvider.Timer.Fire();
            Assert.Equal(removeCallsAfterStop, defender.RemoveCallCount);
        }
        finally
        {
            defender.ReleaseRemoval();
            stopTask ??= service.StopAsync();
            try
            {
                await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (InvalidOperationException)
            {
            }
            service.Dispose();
        }
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

    private static void WriteExpiredState(
        TestDirectories directories,
        DateTimeOffset utcNow,
        string addedPath)
    {
        ApplicationUpdateScanProtectionState state = new()
        {
            ProtectionId = Guid.NewGuid().ToString("N"),
            ApplicationDirectory = directories.ApplicationDirectory,
            UpdateRoot = directories.UpdateRoot,
            AddedPaths = [addedPath],
            CreatedAtUtc = utcNow.AddMinutes(-2),
            ExpiresAtUtc = utcNow.AddMinutes(-1),
            CallerSid = directories.Context.UserSid,
        };
        File.WriteAllText(
            Path.Combine(directories.StateDirectory, state.ProtectionId + ".json"),
            JsonConvert.SerializeObject(state));
    }

    private static void WriteRecoveryJournal(string stateDirectory, string fileName, string path)
    {
        File.WriteAllLines(
            Path.Combine(stateDirectory, fileName),
            [Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(path))]);
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

    private sealed class BlockingDefenderExclusionManager : IDefenderExclusionManager
    {
        private readonly TaskCompletionSource _firstRemovalStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _releaseRemoval = new(initialState: false);
        private int _removeCallCount;

        public Task FirstRemovalStarted => _firstRemovalStarted.Task;

        public int RemoveCallCount => Volatile.Read(ref _removeCallCount);

        public DefenderExclusionChangeResult AddPaths(IReadOnlyCollection<string> paths, string recoveryJournalPath)
        {
            return DefenderExclusionChangeResult.Succeeded(paths.ToArray(), []);
        }

        public DefenderExclusionChangeResult RemovePaths(IReadOnlyCollection<string> paths)
        {
            Interlocked.Increment(ref _removeCallCount);
            _firstRemovalStarted.TrySetResult();
            _releaseRemoval.Wait();
            return DefenderExclusionChangeResult.Succeeded(paths.ToArray(), []);
        }

        public void ReleaseRemoval()
        {
            _releaseRemoval.Set();
        }
    }

    private sealed class ControllableTimeProvider : TimeProvider
    {
        private int _createTimerCallCount;

        public ControllableTimer Timer { get; private set; } = null!;

        public int CreateTimerCallCount => Volatile.Read(ref _createTimerCallCount);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            Interlocked.Increment(ref _createTimerCallCount);
            Timer = new ControllableTimer(callback, state, dueTime, period);
            return Timer;
        }
    }

    private sealed class ControllableTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period) : ITimer
    {
        private int _disposeAsyncCallCount;

        public TimeSpan DueTime { get; } = dueTime;

        public TimeSpan Period { get; } = period;

        public int DisposeAsyncCallCount => Volatile.Read(ref _disposeAsyncCallCount);

        public Exception? DisposeAsyncFailure { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return true;
        }

        public void Fire()
        {
            callback(state);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeAsyncCallCount);
            if (DisposeAsyncFailure != null)
                throw DisposeAsyncFailure;
            return ValueTask.CompletedTask;
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
