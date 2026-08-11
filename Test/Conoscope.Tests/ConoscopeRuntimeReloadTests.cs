using ColorVision.UI;
using Conoscope.Core;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System.IO;

namespace Conoscope.Tests;

public sealed class ConoscopeRuntimeReloadTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"Conoscope-Reload-{Guid.NewGuid():N}");

    [Fact]
    public void ReloadSwitchesMatricesAndSavePathsWhileRunningSnapshotKeepsA()
    {
        Directory.CreateDirectory(tempRoot);
        ConoscopeConfig configA = CreateConfig("A", 1, 2);
        ConoscopeConfig configB = CreateConfig("B", 10, 20);
        var savedConfigs = new List<ConoscopeConfig>();

        using var manager = new ConoscopeManager(
            () => configA,
            config => new ConoscopeGlobalReferenceStore(config, savedConfigs.Add));
        using ConoscopeRuntimeSnapshot runningA = manager.CaptureRuntimeSnapshot();

        Assert.Equal(1, runningA.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));
        Assert.Equal(2, runningA.GlobalReferences.ColorDifferenceReferenceVMat!.At<float>(0, 0));

        manager.BindCurrentConfig(new TestConfigService(configB));

        Assert.Equal(configB.ColorDifferenceReferenceUMatPath, manager.Config.ColorDifferenceReferenceUMatPath);
        Assert.Equal(10, manager.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));
        Assert.Equal(20, manager.GlobalReferences.ColorDifferenceReferenceVMat!.At<float>(0, 0));
        Assert.Equal(1, runningA.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));

        using Mat saveA = new(1, 1, MatType.CV_32FC1, Scalar.All(31));
        runningA.GlobalReferences.SaveContrastReference(ContrastReferenceKind.Black, saveA, "saved-a");

        using (ConoscopeRuntimeSnapshot nextB = manager.CaptureRuntimeSnapshot())
        using (Mat saveB = new(1, 1, MatType.CV_32FC1, Scalar.All(41)))
        {
            nextB.GlobalReferences.SaveContrastReference(ContrastReferenceKind.Black, saveB, "saved-b");
        }

        Assert.Equal([configA, configB], savedConfigs);
        Assert.Equal(31, ReadValue(configA.ContrastBlackReferenceYMatPath));
        Assert.Equal(41, ReadValue(configB.ContrastBlackReferenceYMatPath));
        Assert.Equal("saved-a", configA.ContrastBlackReferenceDisplayName);
        Assert.Equal("saved-b", configB.ContrastBlackReferenceDisplayName);
    }

    [Fact]
    public async Task SlowGenerationBCannotOverwriteFasterGenerationCAndItsStoreIsDisposed()
    {
        Directory.CreateDirectory(tempRoot);
        ConoscopeConfig configA = CreateConfig("race-A", 1, 2);
        ConoscopeConfig configB = CreateConfig("race-B", 10, 20);
        ConoscopeConfig configC = CreateConfig("race-C", 100, 200);
        ConoscopeConfig current = configA;
        using var bStoreEntered = new ManualResetEventSlim();
        using var releaseBStore = new ManualResetEventSlim();
        ConoscopeGlobalReferenceStore? bStore = null;

        using var manager = new ConoscopeManager(
            () => Volatile.Read(ref current),
            config =>
            {
                var store = new ConoscopeGlobalReferenceStore(config);
                if (string.Equals(config.ColorDifferenceReferenceUMatPath, configB.ColorDifferenceReferenceUMatPath, StringComparison.OrdinalIgnoreCase))
                {
                    bStore = store;
                    bStoreEntered.Set();
                    releaseBStore.Wait();
                }
                return store;
            });

        Volatile.Write(ref current, configB);
        Task slowB = Task.Run(() => manager.BindCurrentConfig(new TestConfigService(configB)));
        Assert.True(bStoreEntered.Wait(TimeSpan.FromSeconds(5)));

        Volatile.Write(ref current, configC);
        manager.BindCurrentConfig(new TestConfigService(configC));
        releaseBStore.Set();
        await slowB.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(configC.ColorDifferenceReferenceUMatPath, manager.Config.ColorDifferenceReferenceUMatPath);
        Assert.Equal(100, manager.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));
        Assert.NotNull(bStore);
        Assert.Null(bStore!.ColorDifferenceReferenceUMat);
        Assert.Null(bStore.ColorDifferenceReferenceVMat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonEmptyUnreadableReferencePathIsReportedAndKeepsValidA(bool corruptExistingFile)
    {
        Directory.CreateDirectory(tempRoot);
        ConoscopeConfig configA = CreateConfig("invalid-A", 1, 2);
        ConoscopeConfig configB = CreateConfig("invalid-B", 10, 20);
        string invalidPath = Path.Combine(tempRoot, corruptExistingFile ? "corrupt-u.bin" : "missing-u.bin");
        if (corruptExistingFile)
            File.WriteAllText(invalidPath, "not a matrix");
        configB.ColorDifferenceReferenceUMatPath = invalidPath;
        string officialPath = Path.Combine(tempRoot, "ColorVisionConfig.json");
        string importPath = Path.Combine(tempRoot, "invalid.cvsettings");
        string backupPath = Path.Combine(tempRoot, "Backup");
        Directory.CreateDirectory(backupPath);
        WriteConfig(officialPath, configA);
        WriteConfig(importPath, configB);
        var handler = new ConfigHandler
        {
            ConfigFilePath = officialPath,
            BackupFolderPath = backupPath,
            ConfigDIFileName = "ConoscopeReload",
            IsAutoSave = false,
        };
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        ConoscopeConfig loadedA = handler.GetRequiredService<ConoscopeConfig>();

        using var manager = new ConoscopeManager(
            () => loadedA,
            config => new ConoscopeGlobalReferenceStore(config));
        ConfigReloadResult initialBind = handler.RegisterReloadParticipants(manager);
        Assert.True(initialBind.Succeeded, initialBind.BuildFailureSummary());
        ConoscopeGlobalReferenceStore validStoreA = manager.GlobalReferences;

        ConfigReloadResult result = handler.ImportConfigsWithResult(importPath);

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Equal(ConfigReloadFailureKind.Participant, failure.Kind);
        Assert.Equal(nameof(ConoscopeManager), failure.OwnerName);
        Assert.Same(validStoreA, manager.GlobalReferences);
        Assert.Equal(loadedA.ColorDifferenceReferenceUMatPath, manager.Config.ColorDifferenceReferenceUMatPath);
        Assert.Equal(invalidPath, handler.GetRequiredService<ConoscopeConfig>().ColorDifferenceReferenceUMatPath);
        Assert.Equal(1, manager.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));
        Assert.Equal(2, manager.GlobalReferences.ColorDifferenceReferenceVMat!.At<float>(0, 0));
    }

    [Fact]
    public void ExplicitEmptyReferencePathsCommitAValidClearGeneration()
    {
        Directory.CreateDirectory(tempRoot);
        ConoscopeConfig configA = CreateConfig("clear-A", 1, 2);
        ConoscopeConfig configB = CreateConfig("clear-B", 10, 20);
        configB.ColorDifferenceReferenceUMatPath = string.Empty;
        configB.ColorDifferenceReferenceVMatPath = string.Empty;

        using var manager = new ConoscopeManager(
            () => configA,
            config => new ConoscopeGlobalReferenceStore(config));

        manager.BindCurrentConfig(new TestConfigService(configB));

        Assert.Same(configB, manager.Config);
        Assert.Null(manager.GlobalReferences.ColorDifferenceReferenceUMat);
        Assert.Null(manager.GlobalReferences.ColorDifferenceReferenceVMat);
        Assert.False(manager.GlobalReferences.HasColorDifferenceReference);
    }

    private ConoscopeConfig CreateConfig(string name, float u, float v)
    {
        string directory = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(directory);
        string uPath = Path.Combine(directory, "u.bin");
        string vPath = Path.Combine(directory, "v.bin");
        using Mat uMat = new(1, 1, MatType.CV_32FC1, Scalar.All(u));
        using Mat vMat = new(1, 1, MatType.CV_32FC1, Scalar.All(v));
        ConoscopeReferenceMatSerializer.Save(uPath, uMat);
        ConoscopeReferenceMatSerializer.Save(vPath, vMat);

        return new ConoscopeConfig
        {
            ColorDifferenceReferenceUMatPath = uPath,
            ColorDifferenceReferenceVMatPath = vPath,
            ContrastBlackReferenceYMatPath = Path.Combine(directory, "black.bin")
        };
    }

    private static float ReadValue(string filePath)
    {
        using Mat mat = ConoscopeReferenceMatSerializer.Load(filePath);
        return mat.At<float>(0, 0);
    }

    private static void WriteConfig(string path, ConoscopeConfig config)
    {
        var root = new JObject
        {
            [nameof(ConoscopeConfig)] = JObject.FromObject(config),
        };
        File.WriteAllText(path, root.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }

    private sealed class TestConfigService : IConfigService
    {
        private readonly ConoscopeConfig config;

        public TestConfigService(ConoscopeConfig config)
        {
            this.config = config;
        }

        public IConfig GetRequiredService(Type type) => type == typeof(ConoscopeConfig)
            ? config
            : throw new InvalidOperationException(type.FullName);

        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));

        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }
}
