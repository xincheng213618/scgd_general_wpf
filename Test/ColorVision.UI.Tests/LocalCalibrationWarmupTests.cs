using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using cvColorVision;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public sealed class LocalCalibrationWarmupTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void SmallExitPlansSurviveRestartBeyondThirtyTwoAndAnEmptySnapshotReplacesThem()
    {
        const string oldConfig = """{"Enabled":false,"RecentTemplates":[{"DeviceCode":"left","TemplateName":"stale"}]}""";
        var config = Assert.IsType<LocalCalibrationWarmupConfig>(JsonConvert.DeserializeObject<LocalCalibrationWarmupConfig>(oldConfig));
        Assert.False(config.Enabled);
        Assert.Empty(config.GetExitPlans("left"));
        var plans = Enumerable.Range(0, 35).Select(index => new LocalCalibrationWarmupPlan(
            index < 4 ? "left" : "right",
            [BudgetFile("dark-" + index, 2)])).ToArray();
        config.SetExitPlans(plans, 70);
        var restored = Assert.IsType<LocalCalibrationWarmupConfig>(
            JsonConvert.DeserializeObject<LocalCalibrationWarmupConfig>(JsonConvert.SerializeObject(config)));
        Assert.False(restored.Enabled);
        Assert.Equal(plans.Take(4).Select(plan => plan.Files.Single()), restored.GetExitPlans("left").Select(plan => plan.Files.Single()));
        Assert.Equal(plans.Skip(4).Select(plan => plan.Files.Single()), restored.GetExitPlans("right").Select(plan => plan.Files.Single()));
        restored.SetExitPlans([], 70);
        Assert.Empty(restored.GetExitPlans("left"));
        Assert.Empty(restored.GetExitPlans("right"));
    }

    [Fact]
    public void ExitBudgetChargesSharedVersionsOnlyOnceAndSkipsOverBudgetPlansAsAWhole()
    {
        var config = new LocalCalibrationWarmupConfig();
        var shared = BudgetFile("shared", 60);
        var alias = shared with { DisplayName = "alias", FullPath = shared.FullPath.ToUpperInvariant() };
        var other = BudgetFile("other", 25);
        var ten = BudgetFile("ten", 10);
        var newest = new LocalCalibrationWarmupPlan("camera-A", [shared, other]);
        var same = new LocalCalibrationWarmupPlan("camera-B", [alias, alias]);
        var oversized = new LocalCalibrationWarmupPlan("too-large", [ten, BudgetFile("another-ten", 10)]);
        var exactFit = new LocalCalibrationWarmupPlan("fits", [ten, BudgetFile("five", 5)]);
        var overByOne = new LocalCalibrationWarmupPlan("one-more", [BudgetFile("one", 1)]);

        config.SetExitPlans([newest, same, oversized, exactFit, overByOne], 100);
        Assert.Equal(newest.Files, Assert.Single(config.GetExitPlans("camera-A")).Files);
        Assert.Equal(same.Files, Assert.Single(config.GetExitPlans("camera-B")).Files);
        Assert.Empty(config.GetExitPlans("too-large"));
        Assert.Equal(exactFit.Files, Assert.Single(config.GetExitPlans("fits")).Files);
        Assert.Empty(config.GetExitPlans("one-more"));
        config.SetExitPlans([newest, same], 0);
        Assert.Empty(config.GetExitPlans("camera-A"));
        Assert.Empty(config.GetExitPlans("camera-B"));
    }

    [Fact]
    public void AnAssetWithAnotherTypeOrContentCostsExtraAndUnmeasuredPlansAreSkipped()
    {
        var config = new LocalCalibrationWarmupConfig();
        var asset = BudgetFile("same-path", 20);
        var newest = new LocalCalibrationWarmupPlan("current", [asset]);
        var changed = new LocalCalibrationWarmupPlan("revised", [asset with { Sha256 = new string('B', 64) }]);
        var differentType = new LocalCalibrationWarmupPlan("other-type", [asset with { Type = CalibrationType.Uniformity }]);
        var missingDigest = new LocalCalibrationWarmupPlan("no-digest", [BudgetFile("unknown", 1) with { Sha256 = null }]);
        var missingSize = new LocalCalibrationWarmupPlan("no-estimate", [BudgetFile("unknown-size", 0)]);
        var next = new LocalCalibrationWarmupPlan("remaining", [BudgetFile("small", 5)]);

        config.SetExitPlans([newest, changed, differentType, missingDigest, missingSize, next], 25);
        Assert.Single(config.GetExitPlans("current"));
        Assert.Empty(config.GetExitPlans("revised"));
        Assert.Empty(config.GetExitPlans("other-type"));
        Assert.Empty(config.GetExitPlans("no-digest"));
        Assert.Empty(config.GetExitPlans("no-estimate"));
        Assert.Single(config.GetExitPlans("remaining"));
    }

    private static LocalCalibrationWarmupFile BudgetFile(string name, ulong bytes)
        => new(CalibrationType.DarkNoise, name, Path.Combine(Path.GetTempPath(), name + ".dat"), bytes, new string('A', 64));

    [Fact]
    public void StartupReappliesThePersistedBudgetToTheNativeCache() => WpfTestHost.Invoke(() =>
    {
        using var files = new CalibrationFiles();
        IConfigService previous = ConfigService.Instance;
        ulong original = OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes;
        const int configuredMiB = 48;
        try
        {
            TestEnvironment.SetConfig(new ConfigHandler { ConfigFilePath = files.ConfigurationPath });
            LocalCalibrationWarmupConfig.SetMemoryBudgetMiB(configuredMiB);
            OpenCVCalibration.SetCalibrationSharedCacheBudget(256UL << 20);

            var restarted = new ConfigHandler { ConfigFilePath = files.ConfigurationPath };
            restarted.LoadConfigs();
            TestEnvironment.SetConfig(restarted);
            Assert.Equal(configuredMiB, restarted.GetRequiredService<LocalCalibrationWarmupConfig>().MemoryBudgetMiB);
            new LocalCalibrationBudgetInitializer().InitializeAsync().GetAwaiter().GetResult();
            Assert.Equal((ulong)configuredMiB << 20, OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes);
        }
        finally
        {
            TestEnvironment.SetConfig(previous);
            OpenCVCalibration.SetCalibrationSharedCacheBudget(original);
        }
    });

    [Fact]
    public void ExitSnapshotReflectsNativeContextEvictionAndExplicitRelease()
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("left");
        var a = files.Create("a");
        var b = files.Create("b");
        var c = files.Create("c");
        manager.Prepare(a.Files, a.Name);
        manager.Prepare(b.Files, b.Name);
        manager.Prepare(a.Files, a.Name);
        manager.Prepare(c.Files, c.Name);

        var plans = manager.CaptureWarmupPlans().Select(entry => entry.Plan).ToArray();
        Assert.Equal(new[] { c.Files[0].FullPath, a.Files[0].FullPath }, plans.Select(plan => plan.Files.Single().FullPath));
        Assert.All(plans, plan => Assert.Equal("left", plan.DeviceCode));
        Assert.Equal(0U, Entry(b)?.ActiveOwnerCount);
        manager.ReleaseCache();
        Assert.Empty(manager.CaptureWarmupPlans());
        manager.Prepare(b.Files, b.Name);
        manager.Dispose();
        Assert.Empty(manager.CaptureWarmupPlans());
    }

    [Fact]
    public void NormalShutdownPersistsResidentPlansAndClearsReleasedPlansOnNextExit()
        => WpfTestHost.Invoke(() =>
    {
        using var files = new CalibrationFiles();
        string path = files.ConfigurationPath;
        IConfigService previous = ConfigService.Instance;
        try
        {
            TestEnvironment.SetConfig(new ConfigHandler { ConfigFilePath = path, IsAutoSave = true });
            using var devices = new CurrentCameraDefaultsScope();
            using var manager = new LocalCalibrationCacheManager(devices.Camera.Code);
            typeof(DeviceCamera).GetField("<LocalCalibrationCacheManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(devices.Camera, manager);
            var dark = files.Create("applied");
            var prepared = manager.Prepare(dark.Files, dark.Name);
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 1, Height = 1, Channels = 1, SourceBpp = 16 }, sizeof(ushort), 0);
            prepared.Apply(frame, CalibrationOutputMode.RawOnly);
            Assert.False(File.Exists(path));

            LocalCalibrationWarmupConfig.SaveAtApplicationExit();
            var restored = Assert.Single(ReadSavedPlans());
            var saved = Assert.Single(restored.Files);
            Assert.Equal(CalibrationType.DarkNoise, saved.Type);
            Assert.Equal(dark.Files[0].DisplayName, saved.DisplayName);
            Assert.Equal(dark.Files[0].FullPath, saved.FullPath);
            Assert.Equal(Entry(dark)!.EstimatedMemoryBytes, saved.EstimatedMemoryBytes);
            Assert.True(saved.EstimatedMemoryBytes > 0);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(saved.FullPath))), saved.Sha256);
            manager.ReleaseCache();
            Assert.Single(ReadSavedPlans());
            LocalCalibrationWarmupConfig.SaveAtApplicationExit();
            Assert.Empty(ReadSavedPlans());

            LocalCalibrationWarmupPlan[] ReadSavedPlans()
            {
                var restarted = new ConfigHandler { ConfigFilePath = path };
                restarted.LoadConfigs();
                return restarted.GetRequiredService<LocalCalibrationWarmupConfig>().GetExitPlans(devices.Camera.Code);
            }
        }
        finally { TestEnvironment.SetConfig(previous); }
    });

    [Fact]
    public void ExitSnapshotKeepsTheActualRawOnlySubsetWhenTheCompleteColorPlanWasEvicted()
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("color-subset");
        var dark = files.Create("gain");
        var color = files.Create("color", CalibrationType.LumFourColor, """{"a":1,"b":0,"c":0,"d":0,"e":1,"f":0,"g":0,"h":0,"i":1}""");
        var other = files.Create("other");
        var complete = manager.Prepare([dark.Files[0], color.Files[0]], "mixed");
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 1, Height = 1, Channels = 1, SourceBpp = 16 }, sizeof(ushort), 0);
        complete.Apply(frame, CalibrationOutputMode.RawOnly);
        var before = manager.CaptureWarmupPlans().Select(entry => entry.Plan).ToArray();
        Assert.Equal(new[] { dark.Files[0].FullPath }, before[0].Files.Select(file => file.FullPath));
        Assert.Equal(new[] { dark.Files[0].FullPath, color.Files[0].FullPath }, before[1].Files.Select(file => file.FullPath));

        manager.Prepare(other.Files, other.Name);
        var after = manager.CaptureWarmupPlans().Select(entry => entry.Plan).ToArray();
        Assert.Equal(new[] { other.Files[0].FullPath, dark.Files[0].FullPath }, after.Select(plan => plan.Files.Single().FullPath));
    }

    [Fact]
    public async Task RestoresResidentPlansInOrderAndForegroundReusesTheirNativeContexts()
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("warmup-recent");
        var newest = files.Create("newest");
        var second = files.Create("second");
        var oldest = files.Create("oldest");

        await manager.ScheduleWarmup(_ => Resolved(newest, second), new WarmupClock(),
            _ => Task.FromResult(true), () => true).WaitAsync(TestTimeout);

        Assert.Null(Entry(oldest));
        var newerEntry = Assert.IsType<CalibrationSharedCacheEntry>(Entry(newest));
        var secondEntry = Assert.IsType<CalibrationSharedCacheEntry>(Entry(second));
        Assert.Equal(1U, newerEntry.ActiveOwnerCount);
        Assert.Equal(1U, secondEntry.ActiveOwnerCount);
        Assert.True(secondEntry.LastAccessSequence < newerEntry.LastAccessSequence);

        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata(), sizeof(ushort), 0);
        using var lease = frame.Acquire();
        Marshal.WriteInt16(lease.RawPointer, 4000);
        manager.Execute(new(1, 1, 16, 1), newest.Files, lease.RawPointer, sizeof(ushort), IntPtr.Zero, 0,
            [1, 1, 1], default, CalibrationOutputMode.RawOnly);
        Assert.Equal((short)8000, Marshal.ReadInt16(lease.RawPointer));
        manager.Prepare(second.Files, second.Name);
        await manager.ScheduleWarmup(_ => Resolved(oldest), new WarmupClock(),
            _ => Task.FromResult(true), () => true).WaitAsync(TestTimeout);
        // Recreating a context would acquire the native resource again and change its access sequence.
        Assert.Equal(newerEntry, Entry(newest));
        Assert.Equal(secondEntry, Entry(second));
        Assert.Null(Entry(oldest));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task BackgroundWarmupRefusesPlansOverTheCurrentBudgetButForegroundCanStillPrepare(int budget)
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("background-budget");
        var candidate = files.Create("gain");
        var saved = Saved(candidate);
        Assert.True(saved.Files.Single().EstimatedMemoryBytes > (ulong)budget);
        ulong original = OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes;
        try
        {
            OpenCVCalibration.SetCalibrationSharedCacheBudget((ulong)budget);
            await manager.ScheduleWarmup(_ => Task.FromResult<IReadOnlyList<LocalCalibrationWarmupPlan>>([saved]),
                new WarmupClock(), _ => Task.FromResult(true), () => true).WaitAsync(TestTimeout);
            Assert.Empty(manager.CaptureWarmupPlans());
            Assert.Null(Entry(candidate));

            Assert.True(manager.Prepare(candidate.Files, candidate.Name).HasBasicCalibration);
            Assert.Single(manager.CaptureWarmupPlans());
        }
        finally { OpenCVCalibration.SetCalibrationSharedCacheBudget(original); }
    }

    [Fact]
    public async Task ReplacingASharedAssetChargesItsIncreaseAgainstTheRemainingBudget()
    {
        using var files = new CalibrationFiles();
        using var owner = new LocalCalibrationCacheManager("active-owner");
        using var background = new LocalCalibrationCacheManager("background-owner");
        var gain = files.Create("gain");
        ulong original = OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes;
        try
        {
            OpenCVCalibration.SetCalibrationSharedCacheBudget(ulong.MaxValue);
            owner.Prepare(gain.Files, gain.Name);
            ulong oldSize = Entry(gain)!.EstimatedMemoryBytes;
            ulong used = OpenCVCalibration.GetCalibrationSharedCacheStatistics().EstimatedMemoryBytes;
            File.WriteAllText(gain.Files[0].FullPath, """{"DarkNoiseRatio":3}""" + new string(' ', 4096));
            var replacement = Saved(gain);
            ulong newSize = replacement.Files.Single().EstimatedMemoryBytes;
            Assert.True(newSize > oldSize);
            ulong exactBudget = used + (newSize - oldSize);

            OpenCVCalibration.SetCalibrationSharedCacheBudget(exactBudget - 1);
            await Restore();
            Assert.Empty(background.CaptureWarmupPlans());
            Assert.Equal(oldSize, Entry(gain)!.EstimatedMemoryBytes);

            OpenCVCalibration.SetCalibrationSharedCacheBudget(exactBudget);
            await Restore();
            Assert.Equal(replacement.Files.Single().Sha256,
                Assert.Single(background.CaptureWarmupPlans()).Plan.Files.Single().Sha256);
            Assert.Equal(newSize, Entry(gain)!.EstimatedMemoryBytes);

            Task Restore() => background.ScheduleWarmup(_ => Task.FromResult<IReadOnlyList<LocalCalibrationWarmupPlan>>([replacement]),
                new WarmupClock(), _ => Task.FromResult(true), () => true).WaitAsync(TestTimeout);
        }
        finally { OpenCVCalibration.SetCalibrationSharedCacheBudget(original); }
    }

    [Fact]
    public async Task AReplacedFileCannotWarmAPreviousVersionAtTheSamePathAndTimestamp()
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("changed-file");
        var candidate = files.Create("gain");
        var saved = Saved(candidate);
        string path = candidate.Files.Single().FullPath;
        DateTime modified = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, """{"DarkNoiseRatio":3}""");
        File.SetLastWriteTimeUtc(path, modified);

        await manager.ScheduleWarmup(_ => Task.FromResult<IReadOnlyList<LocalCalibrationWarmupPlan>>([saved]),
            new WarmupClock(), _ => Task.FromResult(true), () => true).WaitAsync(TestTimeout);
        Assert.Empty(manager.CaptureWarmupPlans());
    }

    [Theory]
    [InlineData("foreground")]
    [InlineData("release")]
    [InlineData("close")]
    public async Task ForegroundUseReleaseAndCloseCancelDelayedWarmup(string action)
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("warmup-lifecycle");
        var recent = files.Create("recent");
        var clock = new WarmupClock(automatic: false);
        int idleChecks = 0;
        Task pending = manager.ScheduleWarmup(_ => Resolved(recent), clock,
            _ => { Interlocked.Increment(ref idleChecks); return Task.FromResult(true); }, () => true);
        await clock.DelayStarted.Task.WaitAsync(TestTimeout);

        switch (action)
        {
            case "foreground": manager.Prepare(recent.Files, recent.Name); break;
            case "release": manager.ReleaseCache(); break;
            case "close": manager.OnMeasurementClosed(); break;
        }
        try { await pending.WaitAsync(TestTimeout); }
        catch (OperationCanceledException) when (pending.IsCanceled) { }

        Assert.Equal(0, idleChecks);
        if (action == "foreground") Assert.Equal(1U, Entry(recent)?.ActiveOwnerCount);
        else Assert.Null(Entry(recent));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BusyMachineRetriesWithABoundAndOnlyWarmsWhenIdle(bool eventuallyIdle)
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("warmup-idle");
        var recent = files.Create("idle");
        int attempts = 0;
        await manager.ScheduleWarmup(_ => Resolved(recent), new WarmupClock(),
            _ => Task.FromResult(Interlocked.Increment(ref attempts) >= 2 && eventuallyIdle),
            () => true).WaitAsync(TestTimeout);

        Assert.InRange(attempts, 2, 5);
        if (eventuallyIdle) Assert.Equal(1U, Entry(recent)?.ActiveOwnerCount);
        else Assert.Null(Entry(recent));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LateTemplateResolutionCannotRepopulateAfterReleaseOrDisable(bool release)
    {
        using var files = new CalibrationFiles();
        using var manager = new LocalCalibrationCacheManager("warmup-late");
        var recent = files.Create("late");
        var resolving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<IReadOnlyList<LocalCalibrationWarmupPlan>>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enabled = true;
        Task pending = manager.ScheduleWarmup(_ => { resolving.SetResult(); return result.Task; },
            new WarmupClock(), _ => Task.FromResult(true), () => Volatile.Read(ref enabled));
        await resolving.Task.WaitAsync(TestTimeout);
        if (release) manager.ReleaseCache();
        else Volatile.Write(ref enabled, false);
        result.SetResult([Saved(recent)]);
        await pending.WaitAsync(TestTimeout);
        Assert.Null(Entry(recent));
    }

    private sealed record LocalCalibrationWarmupTemplate(string Name, IReadOnlyList<DeviceCameraCalibrationFile> Files);

    private static LocalCalibrationWarmupPlan Saved(LocalCalibrationWarmupTemplate template)
        => new("warmup-fixture", template.Files.Select(file =>
        {
            byte[] json = File.ReadAllBytes(file.FullPath);
            return new LocalCalibrationWarmupFile(file.CalibrationType, file.DisplayName, file.FullPath,
                (ulong)json.Length, Convert.ToHexString(SHA256.HashData(json)));
        }).ToArray());

    private static Task<IReadOnlyList<LocalCalibrationWarmupPlan>> Resolved(params LocalCalibrationWarmupTemplate[] templates)
        => Task.FromResult<IReadOnlyList<LocalCalibrationWarmupPlan>>(templates.Select(Saved).ToArray());

    private static CalibrationSharedCacheEntry? Entry(LocalCalibrationWarmupTemplate template)
        => LocalCalibrationCacheManager.GetEntries().Entries.SingleOrDefault(
            entry => string.Equals(entry.FilePath, template.Files[0].FullPath, StringComparison.OrdinalIgnoreCase));

    private sealed class WarmupClock(bool automatic = true) : TimeProvider
    {
        internal TaskCompletionSource DelayStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = TimeProvider.System.CreateTimer(callback, state,
                automatic ? TimeSpan.Zero : Timeout.InfiniteTimeSpan, period);
            DelayStarted.TrySetResult();
            return timer;
        }
    }

    private sealed class CalibrationFiles : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-calibration-warmup-");
        public string ConfigurationPath => Path.Combine(directory.FullName, "config.json");

        public LocalCalibrationWarmupTemplate Create(string name, CalibrationType type = CalibrationType.DarkNoise,
            string contents = """{"DarkNoiseRatio":2}""")
        {
            string path = Path.Combine(directory.FullName, name + ".json");
            File.WriteAllText(path, contents);
            return new(name, [new DeviceCameraCalibrationFile(name, type, name, Path.GetFileName(path), path)]);
        }

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
