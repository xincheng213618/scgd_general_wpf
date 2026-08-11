using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class RuntimeConfigOwnerTests
{
    [Fact]
    public void CaptureReturnsDetachedGenerationSnapshot()
    {
        var configA = new SavePathConfig { SavePath = "A" };
        var configB = new SavePathConfig { SavePath = "B" };
        SavePathConfig current = configA;

        using var owner = CreateOwner(() => current);
        RuntimeConfigSnapshot<SavePathConfig> runningTask = owner.CaptureSnapshot();

        configA.SavePath = "A-mutated";
        runningTask.Config.SavePath = "A-task";
        Assert.Equal("A-mutated", owner.Current.SavePath);

        current = configB;
        owner.BindCurrentConfig(new TestConfigService(configB));
        configB.SavePath = "B-mutated";

        Assert.Equal(0, runningTask.Generation);
        Assert.Equal("A-task", runningTask.Config.SavePath);
        Assert.Equal(1, owner.Generation);
        Assert.Equal("B-mutated", owner.Current.SavePath);
        Assert.Equal("B-mutated", owner.Capture().SavePath);
        Assert.Same(configB, owner.Current);
    }

    [Fact]
    public void ReloadFailuresAndSubscriberFailuresAreIsolated()
    {
        var configA = new SavePathConfig { SavePath = "A" };
        var configB = new SavePathConfig { SavePath = "B" };
        SavePathConfig current = configA;
        int errorCount = 0;
        int successfulSubscriberCount = 0;

        using var owner = CreateOwner(
            () => current,
            _ =>
            {
                Interlocked.Increment(ref errorCount);
                throw new InvalidOperationException("error handler failure");
            });
        owner.ConfigurationChanged += (_, _) => throw new InvalidOperationException("subscriber failure");
        owner.ConfigurationChanged += (_, _) => Interlocked.Increment(ref successfulSubscriberCount);

        current = configB;
        Exception? notificationException = Record.Exception(
            () => owner.BindCurrentConfig(new TestConfigService(configB)));

        Assert.Null(notificationException);
        Assert.Equal(1, owner.Generation);
        Assert.Equal("B", owner.Current.SavePath);
        Assert.Equal(1, successfulSubscriberCount);
        Assert.Equal(1, errorCount);

        bool failFactory = true;
        using var failedOwner = CreateOwner(
            () => failFactory ? configA : throw new InvalidOperationException("factory failure"),
            reloadErrorHandler: _ => Interlocked.Increment(ref errorCount));
        failFactory = false;

        Assert.False(failedOwner.Reload());
        Assert.Equal(0, failedOwner.Generation);
        Assert.Equal("A", failedOwner.Current.SavePath);
        Assert.Equal(2, errorCount);
    }

    [Fact]
    public async Task SlowOlderReloadCannotOverwriteNewerGeneration()
    {
        var configA = new SavePathConfig { SavePath = "A" };
        var configB = new SavePathConfig { SavePath = "B" };
        var configC = new SavePathConfig { SavePath = "C" };
        using var slowReloadEntered = new ManualResetEventSlim();
        using var releaseSlowReload = new ManualResetEventSlim();
        int factoryCall = 0;
        var published = new ConcurrentQueue<string>();

        using var owner = CreateOwner(() =>
        {
            int call = Interlocked.Increment(ref factoryCall);
            if (call == 1)
                return configA;
            if (call == 2)
            {
                slowReloadEntered.Set();
                releaseSlowReload.Wait();
                return configB;
            }
            return configC;
        });
        owner.ConfigurationChanged += (_, e) => published.Enqueue(e.Current.SavePath);

        Task<bool> slowReload = Task.Run(owner.Reload);
        Assert.True(slowReloadEntered.Wait(TimeSpan.FromSeconds(5)));

        Assert.True(owner.Reload());
        releaseSlowReload.Set();

        Assert.False(await slowReload.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, owner.Generation);
        Assert.Equal("C", owner.Current.SavePath);
        Assert.Equal(["C"], published);
    }

    [Fact]
    public async Task DisposeWaitsForInFlightReloadAndPreventsItsPublication()
    {
        var configA = new SavePathConfig { SavePath = "A" };
        var configB = new SavePathConfig { SavePath = "B" };
        using var slowReloadEntered = new ManualResetEventSlim();
        using var releaseSlowReload = new ManualResetEventSlim();
        using var disposeStarted = new ManualResetEventSlim();
        int factoryCall = 0;
        int publicationCount = 0;

        var owner = CreateOwner(() =>
        {
            int call = Interlocked.Increment(ref factoryCall);
            if (call == 1)
                return configA;
            if (call == 2)
            {
                slowReloadEntered.Set();
                releaseSlowReload.Wait();
                return configB;
            }
            return configA;
        });
        owner.ConfigurationChanged += (_, _) => Interlocked.Increment(ref publicationCount);

        Task<bool> reload = Task.Run(owner.Reload);
        Assert.True(slowReloadEntered.Wait(TimeSpan.FromSeconds(5)));
        Task dispose = Task.Run(() =>
        {
            disposeStarted.Set();
            owner.Dispose();
        });
        Assert.True(disposeStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(SpinWait.SpinUntil(() => !owner.Reload(), TimeSpan.FromSeconds(5)));
        Assert.False(dispose.IsCompleted);

        int publicationsBeforeRelease = Volatile.Read(ref publicationCount);
        releaseSlowReload.Set();

        Assert.False(await reload.WaitAsync(TimeSpan.FromSeconds(5)));
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(publicationsBeforeRelease, Volatile.Read(ref publicationCount));
        Assert.NotEqual("B", owner.Current.SavePath);
    }

    [Fact]
    public void ConfigHandlerImportBindsOwnerOnceWithoutLegacyDoubleReload()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"RuntimeConfigOwner-{Guid.NewGuid():N}");
        string backupDirectory = Path.Combine(rootDirectory, "Backup");
        string officialPath = Path.Combine(rootDirectory, "ColorVisionConfig.json");
        string importPath = Path.Combine(rootDirectory, "generation-b.cvsettings");
        Directory.CreateDirectory(backupDirectory);

        try
        {
            WriteConfig(officialPath, "A");
            WriteConfig(importPath, "B");
            var handler = new ConfigHandler
            {
                ConfigFilePath = officialPath,
                BackupFolderPath = backupDirectory,
                ConfigDIFileName = "RuntimeOwner",
                IsAutoSave = false,
            };
            Assert.True(handler.LoadConfigsWithResult().Succeeded);

            using var owner = CreateOwner(() => handler.GetRequiredService<SavePathConfig>());
            int notifications = 0;
            owner.ConfigurationChanged += (_, _) => notifications++;

            ConfigReloadResult initialBind = handler.RegisterReloadParticipants(owner);
            ConfigReloadResult imported = handler.ImportConfigsWithResult(importPath);

            Assert.True(initialBind.Succeeded, initialBind.BuildFailureSummary());
            Assert.True(imported.Succeeded, imported.BuildFailureSummary());
            Assert.Equal(1, initialBind.AttemptedParticipantCount);
            Assert.Equal(1, imported.AttemptedParticipantCount);
            Assert.Equal(0, imported.AttemptedLegacySubscriberCount);
            Assert.Equal(2, owner.Generation);
            Assert.Equal(2, notifications);
            Assert.Equal("B", owner.Current.SavePath);
            Assert.Same(handler.GetRequiredService<SavePathConfig>(), owner.Current);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
                Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static void WriteConfig(string path, string savePath)
    {
        var root = new JObject
        {
            [nameof(SavePathConfig)] = JObject.FromObject(new SavePathConfig { SavePath = savePath }),
        };
        File.WriteAllText(path, root.ToString());
    }

    private static RuntimeConfigOwner<SavePathConfig> CreateOwner(
        Func<SavePathConfig> configFactory,
        Action<Exception>? reloadErrorHandler = null)
    {
        return new RuntimeConfigOwner<SavePathConfig>(
            configFactory,
            reloadErrorHandler,
            config => new SavePathConfig { SavePath = config.SavePath });
    }

    public sealed class SavePathConfig : IConfig
    {
        public string SavePath { get; set; } = string.Empty;
    }

    private sealed class TestConfigService : IConfigService
    {
        private readonly SavePathConfig config;

        public TestConfigService(SavePathConfig config)
        {
            this.config = config;
        }

        public IConfig GetRequiredService(Type type) => type == typeof(SavePathConfig)
            ? config
            : throw new InvalidOperationException(type.FullName);

        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));

        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }
}
