using ColorVision.Database;
using ColorVision.Settings.Maintenance;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class ConfigTypeNamePersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ColorVisionConfigTypeNames-{Guid.NewGuid():N}");

    public ConfigTypeNamePersistenceTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostAndSpectrumGuideStateSurvivesAllSavePathsAndReload(bool hostFirst)
    {
        WpfTestHost.Invoke(() =>
        {
            var host = new MainWindowConfig { HasShownNewUserGuide = true, Width = 1234 };
            var spectrum = new Spectrum.MainWindowConfig { EqeEnabled = true, EqeVoltage = 7, Width = 987 };
            var handler = new ConfigHandler
            {
                ConfigFilePath = Path.Combine(_root, "config.json"),
                ConfigDIFileName = "config",
                BackupFolderPath = Path.Combine(_root, "Backup"),
                Configs = new ConcurrentDictionary<Type, IConfig>(1, 31, new WindowConfigOrderComparer(hostFirst))
            };
            handler.Configs[typeof(MainWindowConfig)] = host;
            handler.Configs[typeof(Spectrum.MainWindowConfig)] = spectrum;
            Assert.Equal(hostFirst ? typeof(MainWindowConfig) : typeof(Spectrum.MainWindowConfig), handler.Configs.ToArray()[0].Key);

            Assert.True(handler.TrySave(host, out string error), error);
            handler.Save<Spectrum.MainWindowConfig>();
            AssertWindowConfigs(handler.ConfigFilePath);

            Assert.Equal(ConfigSavePublicationStatus.PersistedAndPublished,
                handler.TrySaveAndPublish(host, () => AssertWindowConfigs(handler.ConfigFilePath), out error));
            handler.SaveConfigs();
            AssertWindowConfigs(handler.ConfigFilePath);

            handler.BackupConfigs();
            AssertWindowConfigs(Assert.Single(Directory.GetFiles(handler.BackupFolderPath, "*.json")));

            var reloaded = new ConfigHandler { ConfigFilePath = handler.ConfigFilePath };
            reloaded.LoadConfigs();
            MainWindowConfig restoredHost = reloaded.GetRequiredService<MainWindowConfig>();
            Assert.True(restoredHost.HasShownNewUserGuide);
            Assert.False(MainWindow.TryRecordNewUserGuideOffer(restoredHost));
            Assert.Equal(1234, restoredHost.Width);
            Spectrum.MainWindowConfig restoredSpectrum = reloaded.GetRequiredService<Spectrum.MainWindowConfig>();
            Assert.True(restoredSpectrum.EqeEnabled);
            Assert.Equal(7, restoredSpectrum.EqeVoltage);
            Assert.Equal(987, restoredSpectrum.Width);
        });
    }

    [Fact]
    public void LegacyDatabaseSettingsAndEncryptedPasswordsSurviveQualifiedSaveAndReload()
    {
        // Synthetic values only. Constructing this configuration does not open a database connection.
        var legacy = new MySqlSetting
        {
            MySqlConfig = new MySqlConfig { Host = "db.example.test", Port = 3310, Database = "field_db", UserName = "test-user", UserPwd = "test-password" },
            MySqlConfigs = [new MySqlConfig { Name = "saved", Host = "backup.example.test", UserPwd = "saved-password" }]
        };
        legacy.Encryption();
        JObject legacyJson = JObject.FromObject(legacy);
        string path = Path.Combine(_root, "legacy.json");
        var original = new JObject
        {
            [nameof(MySqlSetting)] = legacyJson,
            ["UnloadedPluginConfig"] = new JObject { ["Setting"] = "keep" }
        };
        File.WriteAllText(path, original.ToString());
        var handler = new ConfigHandler { ConfigFilePath = path };
        handler.LoadConfigs();
        MySqlSetting loaded = handler.GetRequiredService<MySqlSetting>();
        Assert.Equal("db.example.test", loaded.MySqlConfig.Host);
        Assert.Equal(3310, loaded.MySqlConfig.Port);
        Assert.Equal("field_db", loaded.MySqlConfig.Database);
        Assert.Equal("test-user", loaded.MySqlConfig.UserName);
        Assert.Equal("test-password", loaded.MySqlConfig.UserPwd);
        Assert.Equal("saved-password", loaded.MySqlConfigs[0].UserPwd);

        loaded.MySqlConfig.Port = 3311;
        Assert.True(handler.TrySave(loaded, out string error), error);
        JObject saved = JObject.Parse(File.ReadAllText(path));
        Assert.True(JToken.DeepEquals(legacyJson, saved[nameof(MySqlSetting)]));
        Assert.True(JToken.DeepEquals(original["UnloadedPluginConfig"], saved["UnloadedPluginConfig"]));
        Assert.NotEqual("test-password", (string?)saved[typeof(MySqlSetting).FullName!]!["MySqlConfig"]!["UserPwd"]);
        Assert.Equal("test-password", loaded.MySqlConfig.UserPwd);

        var reloaded = new ConfigHandler { ConfigFilePath = path };
        reloaded.LoadConfigs();
        MySqlSetting current = reloaded.GetRequiredService<MySqlSetting>();
        Assert.Equal(3311, current.MySqlConfig.Port);
        Assert.Equal("db.example.test", current.MySqlConfig.Host);
        Assert.Equal("field_db", current.MySqlConfig.Database);
        Assert.Equal("test-password", current.MySqlConfig.UserPwd);
        Assert.Equal("saved-password", current.MySqlConfigs[0].UserPwd);
    }

    [Fact]
    public void LegacyGuideStateIsReadButQualifiedFalseTakesPrecedence()
    {
        string path = Path.Combine(_root, "legacy-guide.json");
        var json = new JObject { [nameof(MainWindowConfig)] = new JObject { ["HasShownNewUserGuide"] = true } };
        File.WriteAllText(path, json.ToString());
        var handler = new ConfigHandler { ConfigFilePath = path };
        handler.LoadConfigs();
        Assert.True(handler.GetRequiredService<MainWindowConfig>().HasShownNewUserGuide);

        json[typeof(MainWindowConfig).FullName!] = new JObject { ["HasShownNewUserGuide"] = false };
        File.WriteAllText(path, json.ToString());
        handler.LoadConfigs();
        Assert.False(handler.GetRequiredService<MainWindowConfig>().HasShownNewUserGuide);
    }

    [Fact]
    public void WindowResetRemovesBothHostKeysAndPreservesSpectrum()
    {
        string path = Path.Combine(_root, "reset.json");
        File.WriteAllText(path, new JObject
        {
            [nameof(MainWindowConfig)] = new JObject { ["HasShownNewUserGuide"] = true },
            [typeof(MainWindowConfig).FullName!] = new JObject { ["HasShownNewUserGuide"] = true },
            [typeof(Spectrum.MainWindowConfig).FullName!] = new JObject { ["EqeEnabled"] = true }
        }.ToString());
        var reset = new ConfigMaintenanceResetService(path, StorageMaintenanceControl.ResetSectionNames);
        Assert.True(reset.Schedule(reset.Prepare([typeof(MainWindowConfig).FullName!, nameof(MainWindowConfig)])).Succeeded);
        Assert.True(reset.ApplyPending().Succeeded);
        var handler = new ConfigHandler { ConfigFilePath = path };
        handler.LoadConfigs();
        Assert.False(handler.GetRequiredService<MainWindowConfig>().HasShownNewUserGuide);
        Assert.True(handler.GetRequiredService<Spectrum.MainWindowConfig>().EqeEnabled);
    }

    private static void AssertWindowConfigs(string path)
    {
        JObject json = JObject.Parse(File.ReadAllText(path));
        Assert.Null(json[nameof(MainWindowConfig)]);
        Assert.True(json["ColorVision.MainWindowConfig"]!.Value<bool>("HasShownNewUserGuide"));
        Assert.True(json["Spectrum.MainWindowConfig"]!.Value<bool>("EqeEnabled"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class WindowConfigOrderComparer(bool hostFirst) : IEqualityComparer<Type>
    {
        public bool Equals(Type? x, Type? y) => x == y;
        public int GetHashCode(Type type) => (type == typeof(MainWindowConfig)) == hostFirst ? 0 : 1;
    }
}
