using ColorVision.UI.Authorizations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class ConfigHandlerPersistenceTests : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionConfigPersistence-{Guid.NewGuid():N}");

        public ConfigHandlerPersistenceTests()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        [Fact]
        public void ReloadFromDiskRebindsMaterializedConfigsAndLoadsLatestValues()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "initial");

            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.LoadConfigs();
            FirstConfig staleConfig = configHandler.GetRequiredService<FirstConfig>();

            WriteConfig(configFilePath, "after", "after-item", "loaded-later");
            Authorization previousAuthorization = Authorization.Instance;
            try
            {
                configHandler.ReloadFromDisk();

                FirstConfig refreshedConfig = configHandler.GetRequiredService<FirstConfig>();
                Assert.NotSame(staleConfig, refreshedConfig);
                Assert.Equal("after", refreshedConfig.Value);
                Assert.Equal(["after-item"], refreshedConfig.Items);
                Assert.Equal("loaded-later", configHandler.GetRequiredService<SecondConfig>().Value);
                Assert.Same(configHandler.GetRequiredService<Authorization>(), Authorization.Instance);
            }
            finally
            {
                Authorization.Instance = previousAuthorization;
            }
        }

        [Fact]
        public void ReloadFromDisk_NotifiesAfterReplacingMaterializedConfigs()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "initial");

            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.LoadConfigs();
            FirstConfig beforeReload = configHandler.GetRequiredService<FirstConfig>();
            WriteConfig(configFilePath, "after", "after-item", "loaded-later");
            FirstConfig? notifiedConfig = null;
            int notificationCount = 0;
            configHandler.ConfigsReloaded += (_, _) =>
            {
                notificationCount++;
                notifiedConfig = configHandler.GetRequiredService<FirstConfig>();
            };

            configHandler.ReloadFromDisk();

            Assert.Equal(1, notificationCount);
            Assert.NotSame(beforeReload, notifiedConfig);
            Assert.Equal("after", notifiedConfig!.Value);
        }

        [Theory]
        [InlineData("ReloadFromDisk")]
        [InlineData("Import")]
        [InlineData("LoadConfigs")]
        [InlineData("LoadConfigsFromPath")]
        [InlineData("Reload")]
        [InlineData("LoadDefault")]
        public void EverySuccessfulInstallEntryRebindsAuthorizationBeforeCallbacks(string entryPoint)
        {
            Authorization? previousAuthorization = Authorization.Instance;
            try
            {
                string configFilePath = Path.Combine(_rootDirectory, $"{entryPoint}.json");
                string backupDirectory = Path.Combine(_rootDirectory, $"{entryPoint}-Backup");
                Directory.CreateDirectory(backupDirectory);
                WriteAuthorizationConfig(configFilePath, PermissionMode.Guest);
                var configHandler = new ConfigHandler
                {
                    ConfigFilePath = configFilePath,
                    BackupFolderPath = backupDirectory,
                    ConfigDIFileName = entryPoint,
                };
                Assert.True(configHandler.LoadConfigsWithResult().Succeeded);
                Authorization c1 = configHandler.GetRequiredService<Authorization>();
                Assert.Same(c1, Authorization.Instance);

                var participant = new AuthorizationObserver();
                configHandler.ReloadCoordinator.Register(participant);
                Authorization? legacyStaticAuthorization = null;
                Authorization? legacyCurrentAuthorization = null;
                configHandler.ConfigsReloaded += (_, _) =>
                {
                    legacyStaticAuthorization = Authorization.Instance;
                    legacyCurrentAuthorization = configHandler.GetRequiredService<Authorization>();
                };

                ConfigReloadResult result = entryPoint switch
                {
                    "ReloadFromDisk" => ReloadFromDisk(configHandler, configFilePath),
                    "Import" => Import(configHandler),
                    "LoadConfigs" => LoadConfigs(configHandler, configFilePath),
                    "LoadConfigsFromPath" => LoadConfigsFromPath(configHandler),
                    "Reload" => Reload(configHandler, c1),
                    "LoadDefault" => LoadDefault(configHandler, backupDirectory),
                    _ => throw new InvalidOperationException($"Unknown entry point '{entryPoint}'."),
                };

                Assert.True(result.Succeeded, result.BuildFailureSummary());
                Authorization c2 = configHandler.GetRequiredService<Authorization>();
                Assert.NotSame(c1, c2);
                Assert.Equal(PermissionMode.PowerUser, c2.PermissionMode);
                Assert.Same(c2, Authorization.Instance);
                Assert.Same(c2, participant.StaticAuthorization);
                Assert.Same(c2, participant.CurrentAuthorization);
                Assert.Same(c2, legacyStaticAuthorization);
                Assert.Same(c2, legacyCurrentAuthorization);
            }
            finally
            {
                Authorization.Instance = previousAuthorization!;
            }
        }

        [Fact]
        public void LoadConfigs_NotifiesAfterReplacingMaterializedConfigs()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "initial");

            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.LoadConfigs();
            FirstConfig beforeLoad = configHandler.GetRequiredService<FirstConfig>();
            WriteConfig(configFilePath, "after", "after-item", "loaded-later");
            FirstConfig? notifiedConfig = null;
            int notificationCount = 0;
            configHandler.ConfigsReloaded += (_, _) =>
            {
                notificationCount++;
                notifiedConfig = configHandler.GetRequiredService<FirstConfig>();
            };

            configHandler.LoadConfigs();

            Assert.Equal(1, notificationCount);
            Assert.NotSame(beforeLoad, notifiedConfig);
            Assert.Equal("after", notifiedConfig!.Value);
        }

        [Fact]
        public void ReloadFromDiskPreservesLoadedConfigsWhenTheFileIsInvalid()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "initial");

            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.LoadConfigs();
            FirstConfig loadedConfig = configHandler.GetRequiredService<FirstConfig>();
            Authorization loadedAuthorization = configHandler.GetRequiredService<Authorization>();
            File.WriteAllText(configFilePath, "{");

            AggregateException exception = Assert.Throws<AggregateException>(configHandler.ReloadFromDisk);
            Assert.IsType<JsonReaderException>(Assert.Single(exception.InnerExceptions));
            Assert.Equal(ConfigSourceReadStatus.Invalid, configHandler.LastReloadResult.SourceReadStatus);
            Assert.Equal(ConfigRecoveryStatus.NotAttempted, configHandler.LastReloadResult.RecoveryStatus);
            Assert.Same(loadedConfig, configHandler.GetRequiredService<FirstConfig>());
            Assert.Same(loadedAuthorization, configHandler.GetRequiredService<Authorization>());
            Assert.Same(loadedAuthorization, Authorization.Instance);
            Assert.Equal("before", loadedConfig.Value);
            Assert.Equal(["before-item"], loadedConfig.Items);
        }

        private static ConfigReloadResult ReloadFromDisk(ConfigHandler handler, string configFilePath)
        {
            WriteAuthorizationConfig(configFilePath, PermissionMode.PowerUser);
            return handler.ReloadFromDiskWithResult();
        }

        private ConfigReloadResult Import(ConfigHandler handler)
        {
            string importPath = Path.Combine(_rootDirectory, "authorization-import.cvsettings");
            WriteAuthorizationConfig(importPath, PermissionMode.PowerUser);
            return handler.ImportConfigsWithResult(importPath);
        }

        private static ConfigReloadResult LoadConfigs(ConfigHandler handler, string configFilePath)
        {
            WriteAuthorizationConfig(configFilePath, PermissionMode.PowerUser);
            return handler.LoadConfigsWithResult();
        }

        private ConfigReloadResult LoadConfigsFromPath(ConfigHandler handler)
        {
            string alternatePath = Path.Combine(_rootDirectory, "authorization-alternate.json");
            WriteAuthorizationConfig(alternatePath, PermissionMode.PowerUser);
            return handler.LoadConfigsWithResult(alternatePath);
        }

        private static ConfigReloadResult Reload(ConfigHandler handler, Authorization c1)
        {
            c1.PermissionMode = PermissionMode.PowerUser;
            return handler.ReloadWithResult();
        }

        private static ConfigReloadResult LoadDefault(ConfigHandler handler, string backupDirectory)
        {
            string backupPath = Path.Combine(
                backupDirectory,
                $"{handler.ConfigDIFileName}Backup_20260812_010101.json");
            WriteAuthorizationConfig(backupPath, PermissionMode.PowerUser);
            return handler.LoadDefaultConfigsWithResult();
        }

        private static void WriteAuthorizationConfig(string fileName, PermissionMode permissionMode)
        {
            var config = new JObject
            {
                [nameof(Authorization)] = JObject.FromObject(new Authorization
                {
                    PermissionMode = permissionMode,
                }),
            };
            File.WriteAllText(fileName, config.ToString());
        }

        private static void WriteConfig(
            string fileName,
            string firstValue,
            string item,
            string secondValue)
        {
            var config = new JObject
            {
                [nameof(FirstConfig)] = JObject.FromObject(new FirstConfig
                {
                    Value = firstValue,
                    Items = [item],
                }),
                [nameof(SecondConfig)] = JObject.FromObject(new SecondConfig { Value = secondValue }),
            };
            File.WriteAllText(fileName, config.ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }

        public sealed class FirstConfig : IConfig
        {
            public string Value { get; set; } = string.Empty;
            public List<string> Items { get; set; } = new();
        }

        public sealed class SecondConfig : IConfig
        {
            public string Value { get; set; } = string.Empty;
        }

        private sealed class AuthorizationObserver : IConfigReloadParticipant
        {
            public string ConfigReloadName => nameof(AuthorizationObserver);

            public int ConfigReloadOrder => 0;

            public Authorization? StaticAuthorization { get; private set; }

            public Authorization? CurrentAuthorization { get; private set; }

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                StaticAuthorization = Authorization.Instance;
                CurrentAuthorization = currentConfig.GetRequiredService<Authorization>();
            }
        }
    }
}
