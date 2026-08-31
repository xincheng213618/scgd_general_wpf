using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class ConfigMaintenanceResetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ColorVisionMaintenanceReset-{Guid.NewGuid():N}");
    private string ConfigPath => Path.Combine(_root, "ColorVisionConfig.json");
    private ConfigMaintenanceResetService Service => new(ConfigPath, [nameof(AppearanceConfig), "LayoutConfig"]);

    public ConfigMaintenanceResetTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void NoPendingDoesNotCreateOrReplaceConfiguration()
    {
        Assert.Equal(ConfigMaintenanceResetStatus.None, Service.GetPending().Status);
        Assert.Equal(ConfigMaintenanceResetStatus.None, Service.ApplyPending().Status);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void NoPendingDoesNotInvokeStartupAdmission()
    {
        var result = Service.ApplyPending(() => throw new InvalidOperationException("Must not run without a pending reset."));

        Assert.Equal(ConfigMaintenanceResetStatus.None, result.Status);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void DeferredResetPreservesIntentAndBacksUpTheEarlierInstancesFinalSaveOnNextStartup()
    {
        WriteOriginal();
        var earlier = new ConfigHandler { ConfigFilePath = ConfigPath };
        earlier.LoadConfigs();
        var appearance = earlier.GetRequiredService<AppearanceConfig>();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        byte[] pending = File.ReadAllBytes(Service.PendingFilePath);

        var deferred = Service.ApplyPending(() => false);

        Assert.Equal(ConfigMaintenanceResetStatus.Deferred, deferred.Status);
        Assert.True(deferred.Succeeded);
        Assert.False(deferred.ConfigurationChanged);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(pending, File.ReadAllBytes(Service.PendingFilePath));
        Assert.False(Directory.Exists(Service.BackupDirectoryPath));
        Assert.Equal(ConfigMaintenanceResetStatus.Scheduled, Service.GetPending().Status);

        appearance.Value = "earlier-instance-final-save";
        earlier.SaveConfigs();
        byte[] finalSave = File.ReadAllBytes(ConfigPath);
        var applied = Service.ApplyPending(() => true);

        Assert.Equal(ConfigMaintenanceResetStatus.Applied, applied.Status);
        Assert.True(applied.ConfigurationChanged);
        Assert.Equal(finalSave, File.ReadAllBytes(applied.BackupPath!));
        Assert.Null(ReadJson()[nameof(AppearanceConfig)]);
    }

    [Fact]
    public void StartupAdmissionFailureLeavesConfigurationIntentAndBackupUntouched()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        byte[] pending = File.ReadAllBytes(Service.PendingFilePath);

        var result = Service.ApplyPending(() => throw new InvalidOperationException("Process state unavailable."));

        Assert.Equal(ConfigMaintenanceResetStatus.Failed, result.Status);
        Assert.False(result.ConfigurationChanged);
        Assert.Contains("Process state unavailable", result.ErrorMessage);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(pending, File.ReadAllBytes(Service.PendingFilePath));
        Assert.False(Directory.Exists(Service.BackupDirectoryPath));
    }

    [Fact]
    public void DeferredPreparedResetDoesNotAdvanceJournalOrReplaceConfiguration()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        PrepareJournal(original);
        byte[] pending = File.ReadAllBytes(Service.PendingFilePath);
        string backup = BackupPath(ReadPending());
        byte[] backupBytes = File.ReadAllBytes(backup);

        var result = Service.ApplyPending(() => false);

        Assert.Equal(ConfigMaintenanceResetStatus.Deferred, result.Status);
        Assert.False(result.ConfigurationChanged);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(pending, File.ReadAllBytes(Service.PendingFilePath));
        Assert.Equal(backupBytes, File.ReadAllBytes(backup));
    }

    [Fact]
    public void AlreadyAppliedResetDoesNotInvokeStartupAdmissionAgain()
    {
        WriteOriginal();
        Schedule();
        Assert.Equal(ConfigMaintenanceResetStatus.Applied, Service.ApplyPending().Status);
        byte[] original = File.ReadAllBytes(ConfigPath);
        byte[] pending = File.ReadAllBytes(Service.PendingFilePath);

        var result = Service.ApplyPending(() => throw new InvalidOperationException("Must not gate an already applied reset."));

        Assert.Equal(ConfigMaintenanceResetStatus.Applied, result.Status);
        Assert.False(result.ConfigurationChanged);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(pending, File.ReadAllBytes(Service.PendingFilePath));
    }

    [Fact]
    public void ApplyingResetIsNotPublicAndHostAdmissionRegistrationRemainsOptional()
    {
        Assert.Null(typeof(ConfigMaintenanceResetService).GetMethod("ApplyPending", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
        var apply = typeof(ConfigMaintenanceResetService).GetMethod("ApplyPending", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(apply);
        Assert.True(apply.IsAssembly);
        var registration = typeof(ConfigHandler).GetMethod(nameof(ConfigHandler.ConfigureMaintenanceResetSections))!;
        var admission = registration.GetParameters()[1];
        Assert.Equal(typeof(Func<bool>), admission.ParameterType);
        Assert.True(admission.IsOptional);
        Assert.Null(admission.DefaultValue);
    }

    [Fact]
    public void ScheduleLeavesLiveReferencesAndDiskUnchangedThenBacksUpFinalExitSave()
    {
        WriteOriginal();
        byte[] before = File.ReadAllBytes(ConfigPath);
        var handler = new ConfigHandler { ConfigFilePath = ConfigPath };
        handler.LoadConfigs();
        var liveConfig = handler.GetRequiredService<AppearanceConfig>();
        var service = Service;

        Assert.True(service.Schedule(service.Prepare([nameof(AppearanceConfig)])).Succeeded);
        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
        Assert.Same(liveConfig, handler.GetRequiredService<AppearanceConfig>());
        Assert.False(Directory.Exists(service.BackupDirectoryPath));

        liveConfig.Value = "last-exit-save";
        handler.SaveConfigs();
        byte[] finalSave = File.ReadAllBytes(ConfigPath);
        var result = service.ApplyPending();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(ConfigMaintenanceResetStatus.Applied, result.Status);
        Assert.True(result.ConfigurationChanged);
        Assert.Equal(finalSave, File.ReadAllBytes(result.BackupPath!));
        JObject reset = ReadJson();
        Assert.Null(reset[nameof(AppearanceConfig)]);
        Assert.Equal("keep-layout", (string?)reset["LayoutConfig"]?["Value"]);
        Assert.Equal("untouched-secret", (string?)reset["Authorization"]?["Secret"]);
        Assert.Equal("keep-lazy", (string?)reset["NotMaterializedPluginConfig"]?["Value"]);

        var restarted = new ConfigHandler { ConfigFilePath = ConfigPath };
        restarted.LoadConfigs();
        Assert.Equal("default", restarted.GetRequiredService<AppearanceConfig>().Value);
        Assert.NotSame(liveConfig, restarted.GetRequiredService<AppearanceConfig>());
        Assert.Equal("last-exit-save", liveConfig.Value);
    }

    [Fact]
    public void AppliedJournalNeverResetsValuesSavedByTheNewSessionAgain()
    {
        WriteOriginal();
        Schedule();
        var first = Service.ApplyPending();
        Assert.True(first.Succeeded, first.ErrorMessage);
        JObject json = ReadJson();
        json[nameof(AppearanceConfig)] = new JObject { ["Value"] = "new-session" };
        File.WriteAllText(ConfigPath, json.ToString());
        byte[] newBytes = File.ReadAllBytes(ConfigPath);

        var second = Service.ApplyPending();

        Assert.True(second.Succeeded, second.ErrorMessage);
        Assert.False(second.ConfigurationChanged);
        Assert.Equal(newBytes, File.ReadAllBytes(ConfigPath));
        Assert.Single(Directory.EnumerateFiles(Service.BackupDirectoryPath));
    }

    [Fact]
    public void ResetOfTheOnlySectionWritesValidEmptyObjectWithoutBackupFallback()
    {
        File.WriteAllText(ConfigPath, "{\"AppearanceConfig\":{\"Value\":\"old\"}}");
        Schedule();

        var result = Service.ApplyPending();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Empty(ReadJson().Properties());
        string rollingBackups = Path.Combine(_root, "Backup");
        Directory.CreateDirectory(rollingBackups);
        File.WriteAllText(Path.Combine(rollingBackups, "ColorVisionConfigBackup_99999999_999999.json"),
            "{\"AppearanceConfig\":{\"Value\":\"must-not-restore\"}}");
        var handler = new ConfigHandler { ConfigFilePath = ConfigPath, BackupFolderPath = rollingBackups, ConfigDIFileName = "ColorVisionConfig" };
        handler.LoadConfigs();
        Assert.Equal("default", handler.GetRequiredService<AppearanceConfig>().Value);
    }

    [Fact]
    public void IndependentBackupPreservesCompleteOriginalBytesAndIsNotRollingBackup()
    {
        WriteOriginal();
        byte[] original = File.ReadAllBytes(ConfigPath);
        var first = Service.CreateBackup();
        var second = Service.CreateBackup();

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.True(second.Succeeded, second.ErrorMessage);
        Assert.Equal(ConfigMaintenanceResetStatus.BackupCreated, first.Status);
        Assert.NotEqual(first.BackupPath, second.BackupPath);
        Assert.Equal(original, File.ReadAllBytes(first.BackupPath!));
        Assert.Equal(original, File.ReadAllBytes(second.BackupPath!));
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(Service.BackupDirectoryPath, Path.GetDirectoryName(first.BackupPath));
        Assert.False(File.Exists(Service.PendingFilePath));
    }

    [Fact]
    public void UnselectedOpaqueValuesKeepTheirExactNumericAndStringRepresentation()
    {
        const string untouched = "{\"decimal\":0.12345678901234567890123456789,\"exponent\":1e300,\"date\":\"2026-08-31T01:02:03+08:00\",\"nested\":[null,true,123456789012345678901234567890]}";
        File.WriteAllText(ConfigPath, "{\"AppearanceConfig\":{},\"OpaquePluginConfig\":" + untouched + "}");
        Schedule();

        var result = Service.ApplyPending();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("{\"OpaquePluginConfig\":" + untouched + "}", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void PendingResetCanBeCancelledWithoutTouchingConfiguration()
    {
        WriteOriginal();
        byte[] original = File.ReadAllBytes(ConfigPath);
        Schedule();
        Assert.Equal([nameof(AppearanceConfig)], Service.GetPending().SectionNames);

        Assert.Equal(ConfigMaintenanceResetStatus.Cancelled, Service.CancelPending().Status);
        Assert.Equal(ConfigMaintenanceResetStatus.None, Service.ApplyPending().Status);
        Assert.Equal(ConfigMaintenanceResetStatus.None, Service.CancelPending().Status);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.False(Directory.Exists(Service.BackupDirectoryPath));
    }

    [Fact]
    public void SchedulingAgainDoesNotSilentlyOverwritePendingSelection()
    {
        WriteOriginal();
        Schedule();
        byte[] pending = File.ReadAllBytes(Service.PendingFilePath);

        var rejected = Service.Schedule(Service.Prepare(["LayoutConfig"]));

        Assert.False(rejected.Succeeded);
        Assert.Equal(pending, File.ReadAllBytes(Service.PendingFilePath));
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("DeviceConfig")]
    [InlineData("../AppearanceConfig")]
    [InlineData("C:\\Config.json")]
    [InlineData("*")]
    [InlineData("AppearanceConfig ")]
    public void UnapprovedOrPathLikeSectionsAreRejected(string section)
    {
        WriteOriginal();
        Assert.Throws<ArgumentException>(() => Service.Prepare([section]));
        Assert.False(File.Exists(Service.PendingFilePath));
    }

    [Fact]
    public void EmptySelectionIsRejectedAndAllowedSelectionIsImmutable()
    {
        WriteOriginal();
        Assert.Throws<ArgumentException>(() => Service.Prepare([]));
        string[] allowed = [nameof(AppearanceConfig)];
        var service = new ConfigMaintenanceResetService(ConfigPath, allowed);
        allowed[0] = "Authorization";
        Assert.Throws<ArgumentException>(() => service.Prepare(["Authorization"]));
        string[] selection = [nameof(AppearanceConfig)];
        var plan = service.Prepare(selection);
        selection[0] = "Authorization";
        Assert.Equal([nameof(AppearanceConfig)], plan.SectionNames);
    }

    [Fact]
    public void StartupRevalidatesPendingAgainstItsCurrentHostAllowlist()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        var changedPolicy = new ConfigMaintenanceResetService(ConfigPath, ["LayoutConfig"]);

        var result = changedPolicy.ApplyPending();

        Assert.False(result.Succeeded);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.False(Directory.Exists(Service.BackupDirectoryPath));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{} {}")]
    [InlineData("{\"AppearanceConfig\":{},\"AppearanceConfig\":{}}")]
    public void InvalidConfigurationCannotBeBackedUpOrReset(string content)
    {
        WriteOriginal();
        Schedule();
        File.WriteAllText(ConfigPath, content);
        byte[] invalid = File.ReadAllBytes(ConfigPath);

        Assert.False(Service.CreateBackup().Succeeded);
        Assert.False(Service.ApplyPending().Succeeded);
        Assert.Equal(invalid, File.ReadAllBytes(ConfigPath));
        Assert.False(Directory.Exists(Service.BackupDirectoryPath));
    }

    [Fact]
    public void MissingConfigurationCannotPrepareAResetOrProduceABackup()
    {
        Assert.Throws<FileNotFoundException>(() => Service.Prepare([nameof(AppearanceConfig)]));
        Assert.False(Service.CreateBackup().Succeeded);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Id\":\"../outside\",\"State\":\"Scheduled\",\"SectionNames\":[\"AppearanceConfig\"]}")]
    public void CorruptPendingIsReportedAndCanBeCancelledWithoutReset(string pending)
    {
        WriteOriginal();
        byte[] original = File.ReadAllBytes(ConfigPath);
        File.WriteAllText(Service.PendingFilePath, pending);

        Assert.False(Service.GetPending().Succeeded);
        Assert.False(Service.ApplyPending().Succeeded);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(ConfigMaintenanceResetStatus.Cancelled, Service.CancelPending().Status);
        Assert.False(File.Exists(Service.PendingFilePath));
    }

    [Fact]
    public void PendingCannotExpandTheHostAllowlistOrCarryArbitraryFilePaths()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        JObject pending = ReadPending();
        pending["SectionNames"] = new JArray("Authorization");
        SavePending(pending);
        Assert.False(Service.ApplyPending().Succeeded);

        pending["SectionNames"] = new JArray(nameof(AppearanceConfig));
        pending["FilePath"] = Path.Combine(_root, "outside.json");
        SavePending(pending);
        Assert.False(Service.ApplyPending().Succeeded);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.False(Directory.Exists(Service.BackupDirectoryPath));
    }

    [Fact]
    public void BackupDirectoryFailureLeavesConfigurationAndScheduledPlanUntouched()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        byte[] pending = File.ReadAllBytes(Service.PendingFilePath);
        File.WriteAllText(Service.BackupDirectoryPath, "blocks-directory");

        var result = Service.ApplyPending();

        Assert.False(result.Succeeded);
        Assert.False(result.ConfigurationChanged);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(pending, File.ReadAllBytes(Service.PendingFilePath));
    }

    [Fact]
    public void ConflictingBackupIsNeverOverwrittenAndResetDoesNotProceed()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        string backup = BackupPath(ReadPending());
        Directory.CreateDirectory(Service.BackupDirectoryPath);
        File.WriteAllText(backup, "{\"unrelated\":true}");
        byte[] previousBackup = File.ReadAllBytes(backup);

        Assert.False(Service.ApplyPending().Succeeded);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
        Assert.Equal(previousBackup, File.ReadAllBytes(backup));
    }

    [Fact]
    public void PreparedJournalResumesBeforeCommitUsingItsVerifiedBackup()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        PrepareJournal(original);

        var result = Service.ApplyPending();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Null(ReadJson()[nameof(AppearanceConfig)]);
        Assert.Equal(original, File.ReadAllBytes(result.BackupPath!));
        Assert.Equal("Applied", (string?)ReadPending()["State"]);
    }

    [Fact]
    public void PreparedJournalRecognizesAnAlreadyCommittedAtomicReplace()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        byte[] reset = PrepareJournal(original);
        File.WriteAllBytes(ConfigPath, reset);

        var result = Service.ApplyPending();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(result.ConfigurationChanged);
        Assert.Equal(reset, File.ReadAllBytes(ConfigPath));
        Assert.Equal("Applied", (string?)ReadPending()["State"]);
        Assert.Single(Directory.EnumerateFiles(Service.BackupDirectoryPath));
    }

    [Fact]
    public void PreparedJournalRefusesChangedConfigurationInsteadOfResettingAgain()
    {
        WriteOriginal();
        Schedule();
        PrepareJournal(File.ReadAllBytes(ConfigPath));
        File.WriteAllText(ConfigPath, "{\"AppearanceConfig\":{\"Value\":\"later-session\"}}");
        byte[] later = File.ReadAllBytes(ConfigPath);

        var result = Service.ApplyPending();

        Assert.False(result.Succeeded);
        Assert.Contains("changed", result.ErrorMessage);
        Assert.Equal(later, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void PreparedJournalRejectsCorruptedBackupBeforeChangingConfiguration()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        PrepareJournal(original);
        File.WriteAllText(BackupPath(ReadPending()), "{\"changed\":true}");

        var result = Service.ApplyPending();

        Assert.False(result.Succeeded);
        Assert.Contains("SHA-256", result.ErrorMessage);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void FailedAtomicReplaceKeepsOriginalAndCanResumeAfterTheFileIsUnlocked()
    {
        WriteOriginal();
        Schedule();
        byte[] original = File.ReadAllBytes(ConfigPath);
        using (new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = Service.ApplyPending();
            Assert.False(failed.Succeeded);
            Assert.False(failed.ConfigurationChanged);
            Assert.Equal(original, File.ReadAllBytes(ConfigPath));
            Assert.Equal("Prepared", (string?)ReadPending()["State"]);
            Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
        }

        var resumed = Service.ApplyPending();
        Assert.True(resumed.Succeeded, resumed.ErrorMessage);
        Assert.Null(ReadJson()[nameof(AppearanceConfig)]);
        Assert.Equal(original, File.ReadAllBytes(resumed.BackupPath!));
    }

    [Fact]
    public async Task IndependentBackupUsesTheSameFileMutexAsConfigurationSave()
    {
        WriteOriginal();
        using var started = new ManualResetEventSlim();
        Task<ConfigMaintenanceResetResult> backupTask;
        using (ConfigHandler.AcquireSaveFileLock(ConfigPath))
        {
            backupTask = Task.Run(() =>
            {
                started.Set();
                return Service.CreateBackup();
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(backupTask.IsCompleted);
            File.WriteAllText(ConfigPath, "{\"latest\":true}");
        }

        var result = await backupTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(File.ReadAllBytes(ConfigPath), File.ReadAllBytes(result.BackupPath!));
    }

    private void WriteOriginal() => File.WriteAllText(ConfigPath,
        "{\n  \"AppearanceConfig\": {\"Value\":\"custom\"},\n  \"LayoutConfig\": {\"Value\":\"keep-layout\"},\n  \"Authorization\": {\"Secret\":\"untouched-secret\"},\n  \"NotMaterializedPluginConfig\": {\"Value\":\"keep-lazy\"}\n}",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private void Schedule()
    {
        var result = Service.Schedule(Service.Prepare([nameof(AppearanceConfig)]));
        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    private JObject ReadJson() => JObject.Parse(File.ReadAllText(ConfigPath));
    private JObject ReadPending() => JObject.Parse(File.ReadAllText(Service.PendingFilePath));
    private void SavePending(JObject pending) => File.WriteAllText(Service.PendingFilePath, pending.ToString());
    private string BackupPath(JObject pending) => Path.Combine(Service.BackupDirectoryPath, $"ColorVisionConfig.reset-{pending["Id"]}.json");

    private byte[] PrepareJournal(byte[] original)
    {
        JObject reset = ReadJson();
        reset.Remove(nameof(AppearanceConfig));
        byte[] resetBytes = Encoding.UTF8.GetBytes(reset.ToString(Formatting.None));
        JObject pending = ReadPending();
        pending["State"] = "Prepared";
        pending["BeforeSha256"] = Convert.ToHexString(SHA256.HashData(original));
        pending["AfterSha256"] = Convert.ToHexString(SHA256.HashData(resetBytes));
        Directory.CreateDirectory(Service.BackupDirectoryPath);
        File.WriteAllBytes(BackupPath(pending), original);
        SavePending(pending);
        return resetBytes;
    }

    public void Dispose()
    {
        string path = Path.GetFullPath(_root);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(path).StartsWith("ColorVisionMaintenanceReset-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a non-test directory.");
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    public sealed class AppearanceConfig : IConfig
    {
        public string Value { get; set; } = "default";
    }
}
