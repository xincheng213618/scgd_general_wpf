using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class ConfigImportTransactionTests : IDisposable
{
    private const string ConfigName = "TransactionConfig";
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ColorVisionConfigImport-{Guid.NewGuid():N}");
    private readonly string _backupDirectory;
    private readonly string _officialPath;

    public ConfigImportTransactionTests()
    {
        _backupDirectory = Path.Combine(_rootDirectory, "Backup");
        _officialPath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
        Directory.CreateDirectory(_backupDirectory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidImportNeverOverwritesOrRecoversRegardlessOfBackupAvailability(bool hasBackup)
    {
        WriteConfig(_officialPath, "C1");
        ConfigHandler handler = CreateHandler();
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        TransactionConfig c1 = handler.GetRequiredService<TransactionConfig>();
        var owner = new CountingOwner();
        handler.ReloadCoordinator.Register(owner);

        if (hasBackup)
            WriteConfig(Path.Combine(_backupDirectory, $"{ConfigName}Backup_20260812_010101.json"), "Backup");
        string selectedPath = Path.Combine(_rootDirectory, "damaged.cvsettings");
        File.WriteAllText(selectedPath, "{");

        ConfigReloadResult result = handler.ImportConfigsWithResult(selectedPath);

        Assert.False(result.Succeeded);
        Assert.Equal(ConfigSourceReadStatus.Invalid, result.SourceReadStatus);
        Assert.Equal(ConfigRecoveryStatus.NotAttempted, result.RecoveryStatus);
        Assert.Equal(ConfigReloadFailureKind.SourceRead, Assert.Single(result.Failures).Kind);
        Assert.Same(c1, handler.GetRequiredService<TransactionConfig>());
        Assert.Equal("C1", ReadConfigValue(_officialPath));
        Assert.Equal(0, owner.BindCount);
        Assert.Equal(hasBackup ? 1 : 0, Directory.GetFiles(_backupDirectory).Length);
    }

    [Theory]
    [InlineData("trailing-garbage")]
    [InlineData("trailing-comment")]
    [InlineData("leading-comment")]
    [InlineData("inner-comment")]
    [InlineData("second-root")]
    [InlineData("duplicate-property")]
    [InlineData("non-object-root")]
    public void ImportRequiresExactlyOneCompleteObjectAndLeavesC1Untouched(string invalidShape)
    {
        WriteConfig(_officialPath, "C1");
        ConfigHandler handler = CreateHandler();
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        TransactionConfig c1 = handler.GetRequiredService<TransactionConfig>();
        var owner = new CountingOwner();
        handler.ReloadCoordinator.Register(owner);
        string validJson = new JObject
        {
            [nameof(TransactionConfig)] = JObject.FromObject(new TransactionConfig { Value = "C2" }),
        }.ToString(Newtonsoft.Json.Formatting.None);
        string selectedPath = Path.Combine(_rootDirectory, $"{invalidShape}.cvsettings");
        string invalidJson = invalidShape switch
        {
            "trailing-garbage" => $"{validJson} trailing",
            "trailing-comment" => $"{validJson} /* trailing comment */",
            "leading-comment" => $"/* leading comment */ {validJson}",
            "inner-comment" => "{\"TransactionConfig\":{/* inner comment */\"Value\":\"C2\"}}",
            "second-root" => $"{validJson}{validJson}",
            "duplicate-property" => "{\"TransactionConfig\":{\"Value\":\"C2\"},\"TransactionConfig\":{\"Value\":\"C3\"}}",
            "non-object-root" => "[]",
            _ => throw new InvalidOperationException($"Unknown test shape '{invalidShape}'."),
        };
        File.WriteAllText(selectedPath, invalidJson);

        ConfigReloadResult result = handler.ImportConfigsWithResult(selectedPath);

        Assert.False(result.Succeeded);
        Assert.Equal(ConfigSourceReadStatus.Invalid, result.SourceReadStatus);
        Assert.Equal(ConfigRecoveryStatus.NotAttempted, result.RecoveryStatus);
        Assert.Equal(ConfigReloadFailureKind.SourceRead, Assert.Single(result.Failures).Kind);
        Assert.Same(c1, handler.GetRequiredService<TransactionConfig>());
        Assert.Equal("C1", ReadConfigValue(_officialPath));
        Assert.Empty(Directory.GetFiles(_backupDirectory));
        Assert.Equal(0, owner.BindCount);
    }

    [Fact]
    public void ImportBackupFailureReturnsSourceInstallWithoutOverwritingC1()
    {
        WriteConfig(_officialPath, "C1");
        ConfigHandler handler = CreateHandler();
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        TransactionConfig c1 = handler.GetRequiredService<TransactionConfig>();
        var owner = new CountingOwner();
        handler.ReloadCoordinator.Register(owner);
        string selectedPath = Path.Combine(_rootDirectory, "valid-but-backup-blocked.cvsettings");
        WriteConfig(selectedPath, "C2");
        string backupBlocker = Path.Combine(_rootDirectory, "BackupBlocker");
        File.WriteAllText(backupBlocker, "not a directory");
        handler.BackupFolderPath = backupBlocker;

        ConfigReloadResult result = handler.ImportConfigsWithResult(selectedPath);

        Assert.False(result.Succeeded);
        Assert.Equal(ConfigSourceReadStatus.Succeeded, result.SourceReadStatus);
        Assert.Equal(ConfigRecoveryStatus.NotAttempted, result.RecoveryStatus);
        Assert.Equal(ConfigReloadFailureKind.SourceInstall, Assert.Single(result.Failures).Kind);
        Assert.Same(c1, handler.GetRequiredService<TransactionConfig>());
        Assert.Equal("C1", ReadConfigValue(_officialPath));
        Assert.Equal(0, owner.BindCount);
    }

    [Fact]
    public void ValidImportBacksUpOfficialFileThenInstallsAndBindsSelectedConfig()
    {
        WriteConfig(_officialPath, "C1");
        ConfigHandler handler = CreateHandler();
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        TransactionConfig c1 = handler.GetRequiredService<TransactionConfig>();
        var owner = new CountingOwner();
        Assert.True(handler.ReloadCoordinator.RegisterAndBind(owner).Succeeded);
        owner.Reset();
        string selectedPath = Path.Combine(_rootDirectory, "valid.cvsettings");
        WriteConfig(selectedPath, "C2");

        ConfigReloadResult result = handler.ImportConfigsWithResult(selectedPath);

        Assert.True(result.Succeeded, result.BuildFailureSummary());
        Assert.Equal(ConfigSourceReadStatus.Succeeded, result.SourceReadStatus);
        Assert.Equal(ConfigRecoveryStatus.NotRequired, result.RecoveryStatus);
        Assert.NotSame(c1, handler.GetRequiredService<TransactionConfig>());
        Assert.Equal("C2", handler.GetRequiredService<TransactionConfig>().Value);
        Assert.Equal("C2", ReadConfigValue(_officialPath));
        Assert.Equal(1, owner.BindCount);
        Assert.Equal("C2", owner.LastValue);
        string backupPath = Assert.Single(Directory.GetFiles(_backupDirectory));
        Assert.Equal("C1", ReadConfigValue(backupPath));
    }

    [Fact]
    public void ImportAndStrictBackupPreserveUnknownUnmaterializedSections()
    {
        var official = new JObject
        {
            [nameof(TransactionConfig)] = JObject.FromObject(new TransactionConfig { Value = "C1" }),
            ["FutureSection"] = new JObject { ["Marker"] = "old-unknown" },
        };
        File.WriteAllText(_officialPath, official.ToString());
        ConfigHandler handler = CreateHandler();
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        handler.GetRequiredService<TransactionConfig>();
        string selectedPath = Path.Combine(_rootDirectory, "unknown-section.cvsettings");
        var selected = new JObject
        {
            [nameof(TransactionConfig)] = JObject.FromObject(new TransactionConfig { Value = "C2" }),
            ["FutureSection"] = new JObject { ["Marker"] = "new-unknown" },
        };
        File.WriteAllText(selectedPath, selected.ToString());

        ConfigReloadResult result = handler.ImportConfigsWithResult(selectedPath);
        handler.GetRequiredService<TransactionConfig>().Value = "C2-edited";
        handler.SaveConfigs();

        Assert.True(result.Succeeded, result.BuildFailureSummary());
        JObject installed = JObject.Parse(File.ReadAllText(_officialPath));
        Assert.Equal("C2-edited", installed[nameof(TransactionConfig)]![nameof(TransactionConfig.Value)]!.Value<string>());
        Assert.Equal("new-unknown", installed["FutureSection"]!["Marker"]!.Value<string>());
        string backupPath = Assert.Single(Directory.GetFiles(_backupDirectory));
        JObject backup = JObject.Parse(File.ReadAllText(backupPath));
        Assert.Equal("C1", backup[nameof(TransactionConfig)]![nameof(TransactionConfig.Value)]!.Value<string>());
        Assert.Equal("old-unknown", backup["FutureSection"]!["Marker"]!.Value<string>());
    }

    [Theory]
    [InlineData(false, ConfigRecoveryStatus.LoadedDefaults, "")]
    [InlineData(true, ConfigRecoveryStatus.RestoredBackup, "Backup")]
    public void NormalLoadReportsRecoveryWithoutHidingTheSourceReadState(
        bool hasBackup,
        ConfigRecoveryStatus expectedRecovery,
        string expectedValue)
    {
        WriteConfig(_officialPath, "C1");
        ConfigHandler handler = CreateHandler();
        Assert.True(handler.LoadConfigsWithResult().Succeeded);
        if (hasBackup)
            WriteConfig(Path.Combine(_backupDirectory, $"{ConfigName}Backup_20260812_010101.json"), "Backup");
        File.WriteAllText(_officialPath, "{");

        ConfigReloadResult result = handler.LoadConfigsWithResult();

        Assert.True(result.Succeeded, result.BuildFailureSummary());
        Assert.Equal(ConfigSourceReadStatus.Invalid, result.SourceReadStatus);
        Assert.Equal(expectedRecovery, result.RecoveryStatus);
        Assert.Equal(expectedValue, handler.GetRequiredService<TransactionConfig>().Value);
    }

    private ConfigHandler CreateHandler()
    {
        return new ConfigHandler
        {
            ConfigFilePath = _officialPath,
            BackupFolderPath = _backupDirectory,
            ConfigDIFileName = ConfigName,
        };
    }

    private static void WriteConfig(string path, string value)
    {
        var root = new JObject
        {
            [nameof(TransactionConfig)] = JObject.FromObject(new TransactionConfig { Value = value }),
        };
        File.WriteAllText(path, root.ToString());
    }

    private static string ReadConfigValue(string path)
    {
        return JObject.Parse(File.ReadAllText(path))[nameof(TransactionConfig)]![nameof(TransactionConfig.Value)]!
            .Value<string>()!;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    private sealed class CountingOwner : IConfigReloadParticipant
    {
        public string ConfigReloadName => nameof(CountingOwner);

        public int ConfigReloadOrder => 0;

        public int BindCount { get; private set; }

        public string LastValue { get; private set; } = string.Empty;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            BindCount++;
            LastValue = currentConfig.GetRequiredService<TransactionConfig>().Value;
        }

        public void Reset()
        {
            BindCount = 0;
            LastValue = string.Empty;
        }
    }

    public sealed class TransactionConfig : IConfig
    {
        public string Value { get; set; } = string.Empty;
    }
}
