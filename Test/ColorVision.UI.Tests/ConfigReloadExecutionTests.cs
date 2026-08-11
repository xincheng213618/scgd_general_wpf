using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ConfigReloadExecutionTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ColorVisionReloadExecution-{Guid.NewGuid():N}");

    public ConfigReloadExecutionTests()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public void WithResultIsBestEffortAndVoidApiThrowsAggregateAfterEveryCallbackRuns()
    {
        string configPath = WriteConfig("runtime.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        Assert.True(handler.LoadConfigsWithResult().Succeeded);

        var calls = new List<string>();
        handler.ReloadCoordinator.Register(new CallbackParticipant("throws", 10, calls, shouldThrow: true));
        handler.ReloadCoordinator.Register(new CallbackParticipant("after", 20, calls));
        handler.ConfigsReloaded += (_, _) =>
        {
            calls.Add("legacy-throws");
            throw new InvalidOperationException("legacy failed");
        };
        handler.ConfigsReloaded += (_, _) => calls.Add("legacy-after");
        WriteConfig("runtime.json", "C2");
        ConfigReloadResult bestEffort = handler.LoadConfigsWithResult();

        Assert.False(bestEffort.Succeeded);
        Assert.Equal(2, bestEffort.Failures.Count);
        Assert.Equal(
            ["throws:C2", "after:C2", "legacy-throws", "legacy-after"],
            calls);

        calls.Clear();
        WriteConfig("runtime.json", "C3");
        AggregateException exception = Assert.Throws<AggregateException>(handler.LoadConfigs);

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal(
            ["throws:C3", "after:C3", "legacy-throws", "legacy-after"],
            calls);
    }

    [Fact]
    public void ReloadVoidApiAlsoThrowsAggregateOnlyAfterEveryParticipantRuns()
    {
        string configPath = WriteConfig("reload.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        var calls = new List<string>();
        handler.ReloadCoordinator.Register(new CallbackParticipant("throws", 10, calls, shouldThrow: true));
        handler.ReloadCoordinator.Register(new CallbackParticipant("after", 20, calls));

        ConfigReloadResult bestEffort = handler.ReloadWithResult();
        Assert.False(bestEffort.Succeeded);
        Assert.Equal(["throws:C1", "after:C1"], calls);

        calls.Clear();
        AggregateException exception = Assert.Throws<AggregateException>(handler.Reload);

        Assert.Single(exception.InnerExceptions);
        Assert.Equal(["throws:C1", "after:C1"], calls);
    }

    [Fact]
    public void LoadDefaultConfigsUsesTheSameBestEffortAndAggregateContract()
    {
        string configPath = WriteConfig("default-runtime.json", "C1");
        string backupDirectory = Path.Combine(_rootDirectory, "DefaultBackup");
        Directory.CreateDirectory(backupDirectory);
        WriteConfig(Path.Combine("DefaultBackup", "ExecutionBackup_20260812_010101.json"), "Backup");
        var handler = new ConfigHandler
        {
            ConfigFilePath = configPath,
            BackupFolderPath = backupDirectory,
            ConfigDIFileName = "Execution",
        };
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        var calls = new List<string>();
        handler.ReloadCoordinator.Register(new CallbackParticipant("throws", 10, calls, shouldThrow: true));
        handler.ReloadCoordinator.Register(new CallbackParticipant("after", 20, calls));
        handler.ConfigsReloaded += (_, _) =>
        {
            calls.Add("legacy-throws");
            throw new InvalidOperationException("legacy failed");
        };
        handler.ConfigsReloaded += (_, _) => calls.Add("legacy-after");

        ConfigReloadResult bestEffort = handler.LoadDefaultConfigsWithResult();

        Assert.False(bestEffort.Succeeded);
        Assert.Equal(ConfigSourceReadStatus.NotAttempted, bestEffort.SourceReadStatus);
        Assert.Equal(ConfigRecoveryStatus.RestoredBackup, bestEffort.RecoveryStatus);
        Assert.Equal(
            ["throws:Backup", "after:Backup", "legacy-throws", "legacy-after"],
            calls);

        calls.Clear();
        AggregateException exception = Assert.Throws<AggregateException>(handler.LoadDefaultConfigs);

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal(
            ["throws:Backup", "after:Backup", "legacy-throws", "legacy-after"],
            calls);
    }

    [Fact]
    public void ReloadSaveFailureIsReturnedAsSourceInstallAndVoidApiThrowsAggregate()
    {
        string configPath = WriteConfig("save-failure.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        ExecutionConfig c1 = handler.GetRequiredService<ExecutionConfig>();
        handler.ConfigFilePath = _rootDirectory;

        ConfigReloadResult result = handler.ReloadWithResult();

        Assert.False(result.Succeeded);
        Assert.Equal(ConfigSourceReadStatus.NotAttempted, result.SourceReadStatus);
        Assert.Equal(ConfigRecoveryStatus.NotAttempted, result.RecoveryStatus);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Equal(ConfigReloadFailureKind.SourceInstall, failure.Kind);
        Assert.Same(c1, handler.GetRequiredService<ExecutionConfig>());
        AggregateException exception = Assert.Throws<AggregateException>(handler.Reload);
        Assert.Single(exception.InnerExceptions);
        Assert.Equal(ConfigReloadFailureKind.SourceInstall, Assert.Single(handler.LastReloadResult.Failures).Kind);
    }

    [Fact]
    public async Task BackgroundReloadMarshalsTheWholeExecutionBeforeTakingTheGate()
    {
        string configPath = WriteConfig("dispatcher-gate.json", "C1");
        int uiThreadId = 0;
        var participant = new ThreadRecordingParticipant();
        ConfigHandler handler = WpfTestHost.Invoke(() =>
        {
            uiThreadId = Environment.CurrentManagedThreadId;
            var created = new ConfigHandler { ConfigFilePath = configPath };
            Assert.True(created.LoadConfigsWithResult().Succeeded);
            created.GetRequiredService<ExecutionConfig>();
            created.ReloadCoordinator.Register(participant);
            return created;
        });
        var uiActionEntered = new ManualResetEventSlim();
        var allowUiReload = new ManualResetEventSlim();

        Task<ConfigReloadResult> uiReload = Task.Run(() => WpfTestHost.Invoke(() =>
        {
            uiActionEntered.Set();
            if (!allowUiReload.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The UI reload was not released by the test.");
            return handler.ReloadWithResult();
        }));
        Assert.True(uiActionEntered.Wait(TimeSpan.FromSeconds(5)));
        var backgroundAttempted = new ManualResetEventSlim();
        Task<ConfigReloadResult> backgroundReload = Task.Run(() =>
        {
            backgroundAttempted.Set();
            return handler.ReloadWithResult();
        });
        Assert.True(backgroundAttempted.Wait(TimeSpan.FromSeconds(5)));

        await Task.Delay(100);
        allowUiReload.Set();
        ConfigReloadResult[] results = await Task.WhenAll(uiReload, backgroundReload)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(results, result => Assert.True(result.Succeeded, result.BuildFailureSummary()));
        Assert.Equal([uiThreadId, uiThreadId], participant.BindingThreadIds);

        var lateParticipant = new ThreadRecordingParticipant();
        ConfigReloadResult initialBind = await Task.Run(() => handler.RegisterReloadParticipants(lateParticipant));
        ConfigReloadResult repeatedBind = await Task.Run(() => handler.RegisterReloadParticipants(lateParticipant));
        Assert.True(initialBind.Succeeded);
        Assert.True(repeatedBind.Succeeded);
        Assert.Equal(1, initialBind.AttemptedParticipantCount);
        Assert.Equal(0, repeatedBind.AttemptedParticipantCount);
        Assert.Equal([uiThreadId], lateParticipant.BindingThreadIds);
    }

    [Fact]
    public async Task ConcurrentLoadsQueueAcrossInstallBindAndLegacyNotification()
    {
        string firstPath = WriteConfig("first.json", "C1");
        string secondPath = WriteConfig("second.json", "C2");
        var handler = new ConfigHandler { ConfigFilePath = firstPath };
        var firstBindEntered = new ManualResetEventSlim();
        var releaseFirstBind = new ManualResetEventSlim();
        var secondAttempted = new ManualResetEventSlim();
        var observations = new List<(string Before, string After)>();
        var observationLock = new object();
        handler.ReloadCoordinator.Register(new BlockingParticipant(
            firstBindEntered,
            releaseFirstBind,
            observations,
            observationLock));

        Task<ConfigReloadResult> firstLoad = Task.Run(() => handler.LoadConfigsWithResult(firstPath));
        Assert.True(firstBindEntered.Wait(TimeSpan.FromSeconds(5)));
        var installedFirstDictionary = handler.Configs;

        Task<ConfigReloadResult> secondLoad = Task.Run(() =>
        {
            secondAttempted.Set();
            return handler.LoadConfigsWithResult(secondPath);
        });
        Assert.True(secondAttempted.Wait(TimeSpan.FromSeconds(5)));

        Assert.False(SpinWait.SpinUntil(
            () => !ReferenceEquals(installedFirstDictionary, handler.Configs),
            TimeSpan.FromMilliseconds(300)));
        releaseFirstBind.Set();

        ConfigReloadResult[] results = await Task.WhenAll(firstLoad, secondLoad)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(results, result => Assert.True(result.Succeeded, result.BuildFailureSummary()));
        Assert.Equal([("C1", "C1"), ("C2", "C2")], observations);
        Assert.Same(results[1], handler.LastReloadResult);
    }

    [Theory]
    [InlineData("SaveConfigs")]
    [InlineData("SaveSingle")]
    [InlineData("Backup")]
    public async Task PersistenceAndImportShareOneExecutionOwnerWithoutC1WritingAfterC2(string persistenceOperation)
    {
        string configPath = WritePersistenceConfig("persistence-runtime.json", "C1");
        string importPath = WritePersistenceConfig("persistence-import.cvsettings", "C2");
        string blockerPath = Path.Combine(_rootDirectory, "persistence-blocker.json");
        string backupDirectory = Path.Combine(_rootDirectory, "PersistenceBackup");
        Directory.CreateDirectory(backupDirectory);
        var barrier = new PersistenceSerializationBarrier();
        Dispatcher dispatcher = WpfTestHost.Invoke(() => System.Windows.Application.Current.Dispatcher);
        ConfigHandler handler = WpfTestHost.Invoke(() =>
        {
            var created = new ConfigHandler
            {
                ConfigFilePath = configPath,
                BackupFolderPath = backupDirectory,
                ConfigDIFileName = "Persistence",
            };
            Assert.True(created.LoadConfigsWithResult().Succeeded);
            created.GetRequiredService<BlockingPersistenceConfigA>().Barrier = barrier;
            created.GetRequiredService<BlockingPersistenceConfigB>().Barrier = barrier;
            created.ReloadCoordinator.Register(new PersistenceMaterializingParticipant());
            return created;
        });

        Task blockingSave = persistenceOperation == "SaveConfigs"
            ? Task.Run(handler.SaveConfigs)
            : Task.Run(() => handler.SaveConfigs(blockerPath));
        Assert.True(barrier.FirstSerializationEntered.Wait(TimeSpan.FromSeconds(5)));

        int operationPostCount = 0;
        var importPosted = new ManualResetEventSlim();
        var queuedPersistencePosted = new ManualResetEventSlim();
        DispatcherHookEventHandler operationPosted = (_, _) =>
        {
            int count = Interlocked.Increment(ref operationPostCount);
            if (count == 1)
                importPosted.Set();
            else if (count == 2)
                queuedPersistencePosted.Set();
        };
        dispatcher.Hooks.OperationPosted += operationPosted;

        Task<ConfigReloadResult>? import = null;
        Task? queuedPersistence = null;
        bool importWasPosted;
        bool persistenceWasPosted = true;
        try
        {
            import = Task.Run(() => handler.ImportConfigsWithResult(importPath));
            importWasPosted = importPosted.Wait(TimeSpan.FromSeconds(5));
            if (persistenceOperation == "SaveSingle")
                queuedPersistence = Task.Run(() => handler.Save<BlockingPersistenceConfigA>());
            else if (persistenceOperation == "Backup")
                queuedPersistence = Task.Run(handler.BackupConfigs);

            if (queuedPersistence != null)
                persistenceWasPosted = queuedPersistencePosted.Wait(TimeSpan.FromSeconds(5));
        }
        finally
        {
            barrier.ReleaseSerialization.Set();
            dispatcher.Hooks.OperationPosted -= operationPosted;
        }

        Assert.True(importWasPosted, "The import operation was not queued behind the blocked C1 save.");
        Assert.True(persistenceWasPosted, $"{persistenceOperation} did not enter the shared execution owner.");
        await blockingSave.WaitAsync(TimeSpan.FromSeconds(10));
        ConfigReloadResult importResult = await import!.WaitAsync(TimeSpan.FromSeconds(10));
        if (queuedPersistence != null)
            await queuedPersistence.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(importResult.Succeeded, importResult.BuildFailureSummary());
        Assert.Equal("C2", ReadPersistenceValue(configPath, nameof(BlockingPersistenceConfigA)));
        Assert.Equal("C2", ReadPersistenceValue(configPath, nameof(BlockingPersistenceConfigB)));
        Assert.Equal("C2", handler.GetRequiredService<BlockingPersistenceConfigA>().Value);
        Assert.Equal("C2", handler.GetRequiredService<BlockingPersistenceConfigB>().Value);
        if (persistenceOperation == "Backup")
        {
            Assert.Contains(
                Directory.GetFiles(backupDirectory),
                path => ReadPersistenceValue(path, nameof(BlockingPersistenceConfigA)) == "C2"
                    && ReadPersistenceValue(path, nameof(BlockingPersistenceConfigB)) == "C2");
        }
    }

    [Fact]
    public async Task ReentrantReloadIsRejectedWithoutDeadlockAndLaterParticipantStillRuns()
    {
        string configPath = WriteConfig("reentrant.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        var calls = new List<string>();
        handler.ReloadCoordinator.Register(new ReentrantParticipant(handler, calls));
        handler.ReloadCoordinator.Register(new CallbackParticipant("after", 20, calls));

        ConfigReloadResult result = await Task.Run(handler.LoadConfigsWithResult)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Equal(["reentrant", "after:C1"], calls);
    }

    [Fact]
    public async Task ReentrantReloadOnAFlowedTaskContextIsAlsoRejectedWithoutDeadlock()
    {
        string configPath = WriteConfig("task-reentrant.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        handler.ReloadCoordinator.Register(new TaskRunReentrantParticipant(handler));

        ConfigReloadResult result = await Task.Run(handler.LoadConfigsWithResult)
            .WaitAsync(TimeSpan.FromSeconds(5));

        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public void SynchronousParticipantSaveRunsInsideTheCurrentExecutionOwner()
    {
        string configPath = WriteConfig("synchronous-save.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        var calls = new List<string>();
        handler.ReloadCoordinator.Register(new SynchronousSaveParticipant(handler, calls));
        handler.ReloadCoordinator.Register(new CallbackParticipant("after", 20, calls));

        ConfigReloadResult result = handler.LoadConfigsWithResult();

        Assert.True(result.Succeeded, result.BuildFailureSummary());
        Assert.Equal(["saved", "after:C1-saved"], calls);
        Assert.Equal("C1-saved", JObject.Parse(File.ReadAllText(configPath))
            [nameof(ExecutionConfig)]![nameof(ExecutionConfig.Value)]!.Value<string>());
    }

    [Fact]
    public async Task FlowedParticipantSaveIsRejectedWithoutDeadlockAndLaterParticipantStillRuns()
    {
        string configPath = WriteConfig("flowed-save.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath };
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        var calls = new List<string>();
        handler.ReloadCoordinator.Register(new TaskRunSaveParticipant(handler, calls));
        handler.ReloadCoordinator.Register(new CallbackParticipant("after", 20, calls));

        ConfigReloadResult result = await Task.Run(handler.LoadConfigsWithResult)
            .WaitAsync(TimeSpan.FromSeconds(5));

        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Equal(["flowed-save", "after:C1"], calls);
    }

    [Fact]
    public void ReloadFromDiskVoidAggregateKeepsTheAppRecoveryCatchReachable()
    {
        string configPath = WriteConfig("app-recovery.json", "C1");
        var handler = new ConfigHandler { ConfigFilePath = configPath, IsAutoSave = true };
        handler.ReloadCoordinator.Register(new CallbackParticipant("startup-owner", 0, [], shouldThrow: true));
        bool recoveryCatchReached = false;

        try
        {
            handler.ReloadFromDisk();
        }
        catch (AggregateException)
        {
            recoveryCatchReached = true;
            handler.IsAutoSave = false;
        }

        Assert.True(recoveryCatchReached);
        Assert.False(handler.IsAutoSave);
        Assert.False(handler.LastReloadResult.Succeeded);
    }

    private string WriteConfig(string fileName, string value)
    {
        string path = Path.Combine(_rootDirectory, fileName);
        var root = new JObject
        {
            [nameof(ExecutionConfig)] = JObject.FromObject(new ExecutionConfig { Value = value }),
        };
        File.WriteAllText(path, root.ToString());
        return path;
    }

    private string WritePersistenceConfig(string fileName, string value)
    {
        string path = Path.Combine(_rootDirectory, fileName);
        var root = new JObject
        {
            [nameof(BlockingPersistenceConfigA)] = new JObject { ["Value"] = value },
            [nameof(BlockingPersistenceConfigB)] = new JObject { ["Value"] = value },
        };
        File.WriteAllText(path, root.ToString());
        return path;
    }

    private static string ReadPersistenceValue(string path, string configName) =>
        JObject.Parse(File.ReadAllText(path))[configName]!["Value"]!.Value<string>()!;

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    private sealed class CallbackParticipant : IConfigReloadParticipant
    {
        private readonly List<string> _calls;
        private readonly bool _shouldThrow;

        public CallbackParticipant(string name, int order, List<string> calls, bool shouldThrow = false)
        {
            ConfigReloadName = name;
            ConfigReloadOrder = order;
            _calls = calls;
            _shouldThrow = shouldThrow;
        }

        public string ConfigReloadName { get; }

        public int ConfigReloadOrder { get; }

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            _calls.Add($"{ConfigReloadName}:{currentConfig.GetRequiredService<ExecutionConfig>().Value}");
            if (_shouldThrow)
                throw new InvalidOperationException($"{ConfigReloadName} failed");
        }
    }

    private sealed class BlockingParticipant : IConfigReloadParticipant
    {
        private readonly ManualResetEventSlim _firstBindEntered;
        private readonly ManualResetEventSlim _releaseFirstBind;
        private readonly List<(string Before, string After)> _observations;
        private readonly object _observationLock;

        public BlockingParticipant(
            ManualResetEventSlim firstBindEntered,
            ManualResetEventSlim releaseFirstBind,
            List<(string Before, string After)> observations,
            object observationLock)
        {
            _firstBindEntered = firstBindEntered;
            _releaseFirstBind = releaseFirstBind;
            _observations = observations;
            _observationLock = observationLock;
        }

        public string ConfigReloadName => nameof(BlockingParticipant);

        public int ConfigReloadOrder => 0;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            string before = currentConfig.GetRequiredService<ExecutionConfig>().Value;
            if (before == "C1")
            {
                _firstBindEntered.Set();
                if (!_releaseFirstBind.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The first bind was not released by the test.");
            }

            string after = currentConfig.GetRequiredService<ExecutionConfig>().Value;
            lock (_observationLock)
                _observations.Add((before, after));
        }
    }

    private sealed class ReentrantParticipant : IConfigReloadParticipant
    {
        private readonly ConfigHandler _handler;
        private readonly List<string> _calls;

        public ReentrantParticipant(ConfigHandler handler, List<string> calls)
        {
            _handler = handler;
            _calls = calls;
        }

        public string ConfigReloadName => nameof(ReentrantParticipant);

        public int ConfigReloadOrder => 10;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            _calls.Add("reentrant");
            _handler.LoadConfigsWithResult();
        }
    }

    private sealed class TaskRunReentrantParticipant : IConfigReloadParticipant
    {
        private readonly ConfigHandler _handler;

        public TaskRunReentrantParticipant(ConfigHandler handler)
        {
            _handler = handler;
        }

        public string ConfigReloadName => nameof(TaskRunReentrantParticipant);

        public int ConfigReloadOrder => 0;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            Task.Run(_handler.LoadConfigsWithResult).GetAwaiter().GetResult();
        }
    }

    private sealed class SynchronousSaveParticipant : IConfigReloadParticipant
    {
        private readonly ConfigHandler _handler;
        private readonly List<string> _calls;

        public SynchronousSaveParticipant(ConfigHandler handler, List<string> calls)
        {
            _handler = handler;
            _calls = calls;
        }

        public string ConfigReloadName => nameof(SynchronousSaveParticipant);

        public int ConfigReloadOrder => 10;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            currentConfig.GetRequiredService<ExecutionConfig>().Value = "C1-saved";
            _handler.Save<ExecutionConfig>();
            _calls.Add("saved");
        }
    }

    private sealed class TaskRunSaveParticipant : IConfigReloadParticipant
    {
        private readonly ConfigHandler _handler;
        private readonly List<string> _calls;

        public TaskRunSaveParticipant(ConfigHandler handler, List<string> calls)
        {
            _handler = handler;
            _calls = calls;
        }

        public string ConfigReloadName => nameof(TaskRunSaveParticipant);

        public int ConfigReloadOrder => 10;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            _calls.Add("flowed-save");
            Task.Run(() => _handler.Save<ExecutionConfig>()).GetAwaiter().GetResult();
        }
    }

    private sealed class PersistenceMaterializingParticipant : IConfigReloadParticipant
    {
        public string ConfigReloadName => nameof(PersistenceMaterializingParticipant);

        public int ConfigReloadOrder => 0;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            currentConfig.GetRequiredService<BlockingPersistenceConfigA>();
            currentConfig.GetRequiredService<BlockingPersistenceConfigB>();
        }
    }

    internal sealed class PersistenceSerializationBarrier
    {
        private int _serializationCount;

        public ManualResetEventSlim FirstSerializationEntered { get; } = new();

        public ManualResetEventSlim ReleaseSerialization { get; } = new();

        public void Enter()
        {
            if (Interlocked.Increment(ref _serializationCount) != 1)
                return;

            FirstSerializationEntered.Set();
            if (!ReleaseSerialization.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The blocked C1 serialization was not released by the test.");
        }
    }

    private sealed class ThreadRecordingParticipant : IConfigReloadParticipant
    {
        private readonly List<int> _bindingThreadIds = new();

        public string ConfigReloadName => nameof(ThreadRecordingParticipant);

        public int ConfigReloadOrder => 0;

        public IReadOnlyList<int> BindingThreadIds => _bindingThreadIds;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            _bindingThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    public sealed class ExecutionConfig : IConfig
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class BlockingPersistenceConfigA : IConfig
    {
        private string _value = string.Empty;

        [Newtonsoft.Json.JsonIgnore]
        internal PersistenceSerializationBarrier? Barrier { get; set; }

        public string Value
        {
            get
            {
                Barrier?.Enter();
                return _value;
            }
            set => _value = value;
        }
    }

    public sealed class BlockingPersistenceConfigB : IConfig
    {
        private string _value = string.Empty;

        [Newtonsoft.Json.JsonIgnore]
        internal PersistenceSerializationBarrier? Barrier { get; set; }

        public string Value
        {
            get
            {
                Barrier?.Enter();
                return _value;
            }
            set => _value = value;
        }
    }
}
