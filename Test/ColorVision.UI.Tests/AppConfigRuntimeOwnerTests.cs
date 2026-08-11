using ColorVision;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class AppConfigRuntimeOwnerTests
{
    [Fact]
    public async Task InitialDisabledBindingDoesNotRepeatStartupEnforcement()
    {
        var config = new APPConfig { IsMute = false };
        int enforcementCount = 0;
        int persistenceCount = 0;
        using var owner = new AppConfigRuntimeOwner(
            () =>
            {
                enforcementCount++;
                return Task.FromResult<int?>(0);
            },
            _ => persistenceCount++);

        owner.BindCurrentConfig(new AppConfigService(config));
        await owner.WaitForEnforcementIdleAsync();

        Assert.Equal(0, enforcementCount);
        Assert.Equal(0, persistenceCount);

        config.IsMute = true;
        config.IsMute = false;
        await owner.WaitForEnforcementIdleAsync();

        Assert.Equal(1, enforcementCount);
        Assert.Equal(2, persistenceCount);
    }

    [Fact]
    public async Task InitialBindingDoesNotRepeatStartupEnforcementAndC2BecomesTheOnlyEventOwner()
    {
        var c1 = new APPConfig { IsMute = true };
        var c2 = new APPConfig { IsMute = false };
        var service = new AppConfigService(c1);
        var persisted = new List<APPConfig>();
        int enforcementCount = 0;
        var owner = new AppConfigRuntimeOwner(
            () =>
            {
                enforcementCount++;
                return Task.FromResult<int?>(2);
            },
            persisted.Add);

        owner.BindCurrentConfig(service);
        await owner.WaitForEnforcementIdleAsync();
        Assert.Equal(0, enforcementCount);

        service.Current = c2;
        owner.BindCurrentConfig(service);
        await owner.WaitForEnforcementIdleAsync();

        Assert.Equal(1, enforcementCount);
        Assert.Equal([c2, c2], persisted);

        c1.IsMute = false;
        await owner.WaitForEnforcementIdleAsync();
        Assert.Equal(1, enforcementCount);

        c2.IsMute = true;
        c2.IsMute = false;
        await owner.WaitForEnforcementIdleAsync();
        Assert.Equal(2, enforcementCount);
        Assert.Equal([c2, c2, c2, c2], persisted);
    }

    [Fact]
    public async Task RetiredC1FailureCannotRollbackOrPersistC2AndC2GetsItsOwnEnforcement()
    {
        var c1 = new APPConfig { IsMute = true };
        var c2 = new APPConfig { IsMute = false };
        var service = new AppConfigService(c1);
        var persisted = new List<APPConfig>();
        var firstEnforcementStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstEnforcement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int enforcementCount = 0;
        int reportedFailureCount = 0;
        var owner = new AppConfigRuntimeOwner(
            async () =>
            {
                int call = Interlocked.Increment(ref enforcementCount);
                if (call == 1)
                {
                    firstEnforcementStarted.TrySetResult();
                    await releaseFirstEnforcement.Task;
                    throw new InvalidOperationException("C1 enforcement failed after reload");
                }

                return 3;
            },
            persisted.Add,
            enforcementFailed: _ => reportedFailureCount++);

        owner.BindCurrentConfig(service);
        c1.IsMute = false;
        await firstEnforcementStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        service.Current = c2;
        owner.BindCurrentConfig(service);
        releaseFirstEnforcement.TrySetResult();
        await owner.WaitForEnforcementIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, enforcementCount);
        Assert.False(c2.IsMute);
        Assert.Equal(0, reportedFailureCount);
        Assert.Equal([c1, c2, c2], persisted);

        c1.IsMute = true;
        c1.IsMute = false;
        await owner.WaitForEnforcementIdleAsync();
        Assert.Equal(2, enforcementCount);
    }

    [Fact]
    public async Task CurrentC2FailureRollsBackAndPersistsOnlyC2()
    {
        var c1 = new APPConfig { IsMute = true };
        var c2 = new APPConfig { IsMute = false };
        var service = new AppConfigService(c1);
        var persisted = new List<APPConfig>();
        Exception? reportedFailure = null;
        var owner = new AppConfigRuntimeOwner(
            () => Task.FromException<int?>(new InvalidOperationException("unable to acquire mutex")),
            persisted.Add,
            enforcementFailed: ex => reportedFailure = ex);

        owner.BindCurrentConfig(service);
        service.Current = c2;
        owner.BindCurrentConfig(service);
        await owner.WaitForEnforcementIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(c2.IsMute);
        Assert.NotNull(reportedFailure);
        Assert.Equal([c2, c2], persisted);
        Assert.DoesNotContain(c1, persisted);
    }

    [Fact]
    public async Task DisposeInvalidatesAnInFlightGenerationBeforeItCanPersistOrReportFailure()
    {
        var config = new APPConfig { IsMute = true };
        var enforcementStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEnforcement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int persistenceCount = 0;
        int successCount = 0;
        int failureCount = 0;
        var owner = new AppConfigRuntimeOwner(
            async () =>
            {
                enforcementStarted.TrySetResult();
                await releaseEnforcement.Task;
                throw new InvalidOperationException("finished after owner disposal");
            },
            _ => persistenceCount++,
            _ => successCount++,
            _ => failureCount++);

        owner.BindCurrentConfig(new AppConfigService(config));
        config.IsMute = false;
        await enforcementStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, persistenceCount);

        owner.Dispose();
        releaseEnforcement.TrySetResult();
        await owner.WaitForEnforcementIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, persistenceCount);
        Assert.Equal(0, successCount);
        Assert.Equal(0, failureCount);

        config.IsMute = true;
        config.IsMute = false;
        await owner.WaitForEnforcementIdleAsync();
        Assert.Equal(1, persistenceCount);
    }

    [Fact]
    public async Task ImportRebindsFromC1ToC2AndOnlyC2CanEnforceSingleInstanceMode()
    {
        string rootDirectory = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionAppConfigOwner-{Guid.NewGuid():N}"));
        string backupDirectory = Path.Combine(rootDirectory, "Backup");
        string officialPath = Path.Combine(rootDirectory, "ColorVisionConfig.json");
        string importPath = Path.Combine(rootDirectory, "selected.cvsettings");
        Directory.CreateDirectory(backupDirectory);

        try
        {
            WriteAppConfig(officialPath, isMute: true);
            WriteAppConfig(importPath, isMute: false);
            var handler = new ConfigHandler
            {
                ConfigFilePath = officialPath,
                BackupFolderPath = backupDirectory,
                ConfigDIFileName = "AppOwnerConfig",
            };
            Assert.True(handler.LoadConfigsWithResult().Succeeded);
            APPConfig c1 = handler.GetRequiredService<APPConfig>();
            var persisted = new List<APPConfig>();
            int enforcementCount = 0;
            using var owner = new AppConfigRuntimeOwner(
                () =>
                {
                    enforcementCount++;
                    return Task.FromResult<int?>(1);
                },
                config =>
                {
                    persisted.Add(config);
                    handler.Save<APPConfig>();
                });
            Assert.True(handler.RegisterReloadParticipants(owner).Succeeded);

            ConfigReloadResult result = handler.ImportConfigsWithResult(importPath);
            await owner.WaitForEnforcementIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.Succeeded, result.BuildFailureSummary());
            APPConfig c2 = handler.GetRequiredService<APPConfig>();
            Assert.NotSame(c1, c2);
            Assert.False(c2.IsMute);
            Assert.Equal(1, enforcementCount);
            Assert.Equal([c2, c2], persisted);
            string backupPath = Assert.Single(Directory.GetFiles(backupDirectory));
            Assert.True(ReadIsMute(backupPath));

            c1.IsMute = false;
            await owner.WaitForEnforcementIdleAsync();
            Assert.Equal(1, enforcementCount);

            c2.IsMute = true;
            c2.IsMute = false;
            await owner.WaitForEnforcementIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(2, enforcementCount);
            Assert.Equal([c2, c2, c2, c2], persisted);
            Assert.False(ReadIsMute(officialPath));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
                Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static void WriteAppConfig(string path, bool isMute)
    {
        var root = new JObject
        {
            [nameof(APPConfig)] = JObject.FromObject(new APPConfig { IsMute = isMute }),
        };
        File.WriteAllText(path, root.ToString());
    }

    private static bool ReadIsMute(string path) =>
        JObject.Parse(File.ReadAllText(path))[nameof(APPConfig)]![nameof(APPConfig.IsMute)]!.Value<bool>();

    private sealed class AppConfigService : IConfigService
    {
        public AppConfigService(APPConfig current)
        {
            Current = current;
        }

        public APPConfig Current { get; set; }

        public IConfig GetRequiredService(Type type) => type == typeof(APPConfig)
            ? Current
            : throw new InvalidOperationException($"Unexpected config type {type.FullName}.");

        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));

        public void SaveConfigs() => throw new NotSupportedException();

        public void LoadConfigs() => throw new NotSupportedException();

        public void Save<T>() where T : IConfig => throw new NotSupportedException();
    }
}
