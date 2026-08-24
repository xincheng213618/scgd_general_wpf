using ColorVision.UI.Authorizations;
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
            File.WriteAllText(configFilePath, "{");

            Assert.Throws<InvalidOperationException>(configHandler.ReloadFromDisk);
            Assert.Same(loadedConfig, configHandler.GetRequiredService<FirstConfig>());
            Assert.Equal("before", loadedConfig.Value);
            Assert.Equal(["before-item"], loadedConfig.Items);
        }

        [Fact]
        public void TrySavePreservesOtherConfigurationSections()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "keep-second");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            var candidate = new FirstConfig
            {
                Value = "after",
                Items = ["after-item"],
            };

            bool saved = configHandler.TrySave(candidate, out string errorMessage);

            Assert.True(saved, errorMessage);
            Assert.Equal(string.Empty, errorMessage);
            JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
            Assert.Equal("after", persisted[nameof(FirstConfig)]![nameof(FirstConfig.Value)]);
            Assert.Equal("after-item", persisted[nameof(FirstConfig)]![nameof(FirstConfig.Items)]![0]);
            Assert.Equal("keep-second", persisted[nameof(SecondConfig)]![nameof(SecondConfig.Value)]);
            AssertNoTemporaryFiles();
        }

        [Fact]
        public async Task ConcurrentTrySaveCallsOnSameHandlerPreserveBothSections()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "before-second");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };

            for (int round = 0; round < 8; round++)
            {
                string firstValue = $"first-{round}";
                string secondValue = $"second-{round}";
                using var startGate = new Barrier(participantCount: 3);
                Task<(bool Saved, string ErrorMessage)> saveFirst = Task.Run(() =>
                {
                    startGate.SignalAndWait();
                    bool saved = configHandler.TrySave(
                        new FirstConfig { Value = firstValue, Items = [$"item-{round}"] },
                        out string errorMessage);
                    return (saved, errorMessage);
                });
                Task<(bool Saved, string ErrorMessage)> saveSecond = Task.Run(() =>
                {
                    startGate.SignalAndWait();
                    bool saved = configHandler.TrySave(
                        new SecondConfig { Value = secondValue },
                        out string errorMessage);
                    return (saved, errorMessage);
                });

                startGate.SignalAndWait();
                var results = await Task.WhenAll(saveFirst, saveSecond);

                Assert.All(results, result => Assert.True(result.Saved, result.ErrorMessage));
                JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
                Assert.Equal(firstValue, persisted[nameof(FirstConfig)]![nameof(FirstConfig.Value)]);
                Assert.Equal(secondValue, persisted[nameof(SecondConfig)]![nameof(SecondConfig.Value)]);
            }

            AssertNoTemporaryFiles();
        }

        [Fact]
        public async Task ConcurrentTrySaveCallsOnDifferentHandlersPreserveBothSections()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "before-second");
            var firstHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            var secondHandler = new ConfigHandler
            {
                ConfigFilePath = Path.Combine(_rootDirectory, "path-alias", "..", "ColorVisionConfig.json"),
            };

            for (int round = 0; round < 32; round++)
            {
                string firstValue = $"first-{round}";
                string secondValue = $"second-{round}";
                using var startGate = new Barrier(participantCount: 3);
                Task<(bool Saved, string ErrorMessage)> saveFirst = Task.Run(() =>
                {
                    startGate.SignalAndWait();
                    bool saved = firstHandler.TrySave(
                        new FirstConfig { Value = firstValue, Items = [$"item-{round}"] },
                        out string errorMessage);
                    return (saved, errorMessage);
                });
                Task<(bool Saved, string ErrorMessage)> saveSecond = Task.Run(() =>
                {
                    startGate.SignalAndWait();
                    bool saved = secondHandler.TrySave(
                        new SecondConfig { Value = secondValue },
                        out string errorMessage);
                    return (saved, errorMessage);
                });

                startGate.SignalAndWait();
                var results = await Task.WhenAll(saveFirst, saveSecond);

                Assert.All(results, result => Assert.True(result.Saved, result.ErrorMessage));
                JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
                Assert.Equal(firstValue, persisted[nameof(FirstConfig)]![nameof(FirstConfig.Value)]);
                Assert.Equal(secondValue, persisted[nameof(SecondConfig)]![nameof(SecondConfig.Value)]);
            }

            AssertNoTemporaryFiles();
        }

        [Fact]
        public void SaveConfigsRetriesAStaleSnapshotAfterTrySaveCommits()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            File.WriteAllText(
                configFilePath,
                new JObject
                {
                    [nameof(ReentrantSaveConfig)] = JObject.FromObject(new { Value = "old" }),
                }.ToString());
            var liveConfig = new ReentrantSaveConfig("old");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.Configs[typeof(ReentrantSaveConfig)] = liveConfig;
            var nestedStatus = ConfigSavePublicationStatus.NotPersisted;
            var nestedErrorMessage = string.Empty;
            liveConfig.OnNextRead = () =>
            {
                nestedStatus = configHandler.TrySaveAndPublish(
                    new ReentrantSaveConfig("new"),
                    () => liveConfig.SetValue("new"),
                    out nestedErrorMessage);
            };

            configHandler.SaveConfigs();

            Assert.Equal(ConfigSavePublicationStatus.PersistedAndPublished, nestedStatus);
            Assert.Equal(string.Empty, nestedErrorMessage);
            JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
            Assert.Equal("new", persisted[nameof(ReentrantSaveConfig)]![nameof(ReentrantSaveConfig.Value)]);
            Assert.Equal("new", liveConfig.Value);
            AssertNoTemporaryFiles();
        }

        [Fact]
        public void ReloadDuringSnapshotInvalidatesAndRetries()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            File.WriteAllText(
                configFilePath,
                new JObject
                {
                    [nameof(ReentrantSaveConfig)] = JObject.FromObject(new { Value = "reloaded" }),
                }.ToString());
            var liveConfig = new ReentrantSaveConfig("stale");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.Configs[typeof(ReentrantSaveConfig)] = liveConfig;
            liveConfig.OnNextRead = configHandler.ReloadFromDisk;
            int reloadNotificationCount = 0;
            configHandler.ConfigsReloaded += (_, _) => reloadNotificationCount++;
            Authorization previousAuthorization = Authorization.Instance;

            try
            {
                configHandler.SaveConfigs();

                JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
                Assert.Equal("reloaded", persisted[nameof(ReentrantSaveConfig)]![nameof(ReentrantSaveConfig.Value)]);
                Assert.Equal("reloaded", configHandler.GetRequiredService<ReentrantSaveConfig>().Value);
                Assert.Equal(1, reloadNotificationCount);
                AssertNoTemporaryFiles();
            }
            finally
            {
                Authorization.Instance = previousAuthorization;
            }
        }

        [Fact]
        public void TrySaveAndPublishDistinguishesAnInMemoryPublishFailureFromPersistenceFailure()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "keep-second");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };

            ConfigSavePublicationStatus status = configHandler.TrySaveAndPublish(
                new FirstConfig { Value = "persisted", Items = ["persisted-item"] },
                () => throw new InvalidOperationException("Runtime publication failed."),
                out string errorMessage);

            Assert.Equal(ConfigSavePublicationStatus.PersistedButPublishFailed, status);
            Assert.Contains("Runtime publication failed", errorMessage, StringComparison.Ordinal);
            JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
            Assert.Equal("persisted", persisted[nameof(FirstConfig)]![nameof(FirstConfig.Value)]);
            Assert.Equal("keep-second", persisted[nameof(SecondConfig)]![nameof(SecondConfig.Value)]);
            AssertNoTemporaryFiles();
        }

        [Fact]
        public void TrySaveAndPublishRejectsAReentrantSaveFromItsPublicationCallback()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "keep-second");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            var nestedSaved = true;
            var nestedErrorMessage = string.Empty;

            ConfigSavePublicationStatus status = configHandler.TrySaveAndPublish(
                new FirstConfig { Value = "persisted", Items = ["persisted-item"] },
                () => nestedSaved = configHandler.TrySave(
                    new SecondConfig { Value = "must-not-be-written" },
                    out nestedErrorMessage),
                out string errorMessage);

            Assert.Equal(ConfigSavePublicationStatus.PersistedAndPublished, status);
            Assert.Equal(string.Empty, errorMessage);
            Assert.False(nestedSaved);
            Assert.Contains("reentrantly", nestedErrorMessage, StringComparison.OrdinalIgnoreCase);
            JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
            Assert.Equal("persisted", persisted[nameof(FirstConfig)]![nameof(FirstConfig.Value)]);
            Assert.Equal("keep-second", persisted[nameof(SecondConfig)]![nameof(SecondConfig.Value)]);
            AssertNoTemporaryFiles();
        }

        [Fact]
        public void TrySaveAndPublishRejectsAReentrantSaveFromAnAsyncPublicationFlow()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "keep-second");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            Task<(bool Saved, string ErrorMessage)>? nestedSave = null;

            ConfigSavePublicationStatus status = configHandler.TrySaveAndPublish(
                new FirstConfig { Value = "persisted", Items = ["persisted-item"] },
                () =>
                {
                    nestedSave = Task.Run(() =>
                    {
                        bool saved = configHandler.TrySave(
                            new SecondConfig { Value = "must-not-be-written" },
                            out string nestedErrorMessage);
                        return (saved, nestedErrorMessage);
                    });
                    if (!nestedSave.Wait(TimeSpan.FromSeconds(2)))
                        throw new TimeoutException("The nested save did not fail fast.");
                },
                out string errorMessage);

            var nestedResult = nestedSave!.GetAwaiter().GetResult();
            Assert.Equal(ConfigSavePublicationStatus.PersistedAndPublished, status);
            Assert.Equal(string.Empty, errorMessage);
            Assert.False(nestedResult.Saved);
            Assert.Contains("reentrantly", nestedResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
            Assert.Equal("persisted", persisted[nameof(FirstConfig)]![nameof(FirstConfig.Value)]);
            Assert.Equal("keep-second", persisted[nameof(SecondConfig)]![nameof(SecondConfig.Value)]);
            AssertNoTemporaryFiles();
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{\"SecondConfig\":{\"Value\":\"keep\"}} trailing")]
        public void TrySaveRejectsAnInvalidExistingConfigurationFile(string invalidJson)
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            File.WriteAllText(configFilePath, invalidJson);
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };

            bool saved = configHandler.TrySave(
                new FirstConfig { Value = "replacement" },
                out string errorMessage);

            Assert.False(saved);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
            Assert.Equal(invalidJson, File.ReadAllText(configFilePath));
            AssertNoTemporaryFiles();
        }

        [Fact]
        public void TrySaveWriteFailureLeavesExistingBytesUnchangedAndRemovesTemporaryFile()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "keep-second");
            byte[] originalBytes = File.ReadAllBytes(configFilePath);
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };

            bool saved;
            string errorMessage;
            using (new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                saved = configHandler.TrySave(
                    new FirstConfig { Value = "replacement" },
                    out errorMessage);
            }

            Assert.False(saved);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
            Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
            AssertNoTemporaryFiles();
        }

        [Fact]
        public void TrySaveSerializationFailureDoesNotChangeExistingConfigurationFile()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "keep-second");
            byte[] originalBytes = File.ReadAllBytes(configFilePath);
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };

            bool saved = configHandler.TrySave(
                new ThrowingConfig(),
                out string errorMessage);

            Assert.False(saved);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
            Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
            AssertNoTemporaryFiles();
        }

        [Fact]
        public void SaveConfigsSerializationFailureDoesNotCreateAPartialExport()
        {
            string exportFilePath = Path.Combine(_rootDirectory, "Export.cvsettings");
            var configHandler = new ConfigHandler { ConfigFilePath = exportFilePath };
            configHandler.Configs[typeof(FirstConfig)] = new FirstConfig { Value = "serializable" };
            configHandler.Configs[typeof(ThrowingConfig)] = new ThrowingConfig();

            AggregateException exception = Assert.Throws<AggregateException>(() =>
                configHandler.SaveConfigs(exportFilePath));

            Assert.Contains("not saved", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(exportFilePath));
            AssertNoTemporaryFiles();
        }

        [Fact]
        public void TrySaveSecureEncryptionFailureDoesNotMutateTheCandidate()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "before", "before-item", "keep-second");
            byte[] originalBytes = File.ReadAllBytes(configFilePath);
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            var candidate = new PartiallyThrowingSecureConfig
            {
                FirstSecret = "first-plaintext",
                SecondSecret = "second-plaintext",
            };

            bool saved = configHandler.TrySave(candidate, out string errorMessage);

            Assert.False(saved);
            Assert.Contains("Encryption failed", errorMessage, StringComparison.Ordinal);
            Assert.Equal("first-plaintext", candidate.FirstSecret);
            Assert.Equal("second-plaintext", candidate.SecondSecret);
            Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
            AssertNoTemporaryFiles();
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("not-json")]
        [InlineData("[]")]
        public void LoadConfigsRestoresLatestValidBackupWhenTheMainFileIsInvalid(string invalidJson)
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            string backupFolderPath = Path.Combine(_rootDirectory, "Backup");
            Directory.CreateDirectory(backupFolderPath);
            File.WriteAllText(configFilePath, invalidJson);
            WriteConfig(
                Path.Combine(backupFolderPath, "ColorVisionConfigBackup_20260818_120000.json"),
                "backup",
                "backup-item",
                "backup-second");

            var configHandler = new ConfigHandler
            {
                ConfigDIFileName = "ColorVisionConfig",
                ConfigFilePath = configFilePath,
                BackupFolderPath = backupFolderPath,
            };

            configHandler.LoadConfigs();

            FirstConfig restoredConfig = configHandler.GetRequiredService<FirstConfig>();
            Assert.Equal("backup", restoredConfig.Value);
            Assert.Equal(["backup-item"], restoredConfig.Items);
            Assert.Equal("backup-second", configHandler.GetRequiredService<SecondConfig>().Value);
            Assert.Equal("backup", JObject.Parse(File.ReadAllText(configFilePath))[nameof(FirstConfig)]![nameof(FirstConfig.Value)]);
        }

        [Fact]
        public void LoadConfigsSkipsAnInvalidNewerBackupAndRestoresAnOlderValidBackup()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            string backupFolderPath = Path.Combine(_rootDirectory, "Backup");
            Directory.CreateDirectory(backupFolderPath);
            File.WriteAllText(configFilePath, "{");
            WriteConfig(
                Path.Combine(backupFolderPath, "ColorVisionConfigBackup_20260818_120000.json"),
                "older-valid-backup",
                "older-item",
                "older-second");
            File.WriteAllText(
                Path.Combine(backupFolderPath, "ColorVisionConfigBackup_20260818_130000.json"),
                "{");

            var configHandler = new ConfigHandler
            {
                ConfigDIFileName = "ColorVisionConfig",
                ConfigFilePath = configFilePath,
                BackupFolderPath = backupFolderPath,
            };

            configHandler.LoadConfigs();

            FirstConfig restoredConfig = configHandler.GetRequiredService<FirstConfig>();
            Assert.Equal("older-valid-backup", restoredConfig.Value);
            Assert.Equal(["older-item"], restoredConfig.Items);
            Assert.Equal("older-second", configHandler.GetRequiredService<SecondConfig>().Value);
        }

        [Fact]
        public void LoadConfigsUsesDefaultsWhenTheMainFileAndEveryBackupAreInvalid()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            string backupFolderPath = Path.Combine(_rootDirectory, "Backup");
            Directory.CreateDirectory(backupFolderPath);
            File.WriteAllText(configFilePath, string.Empty);
            File.WriteAllText(
                Path.Combine(backupFolderPath, "ColorVisionConfigBackup_20260818_120000.json"),
                "not-json");

            var configHandler = new ConfigHandler
            {
                ConfigDIFileName = "ColorVisionConfig",
                ConfigFilePath = configFilePath,
                BackupFolderPath = backupFolderPath,
            };

            configHandler.LoadConfigs();

            FirstConfig defaultConfig = configHandler.GetRequiredService<FirstConfig>();
            Assert.Equal(string.Empty, defaultConfig.Value);
            Assert.Empty(defaultConfig.Items);
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

        private void AssertNoTemporaryFiles()
        {
            Assert.Empty(Directory.EnumerateFiles(_rootDirectory, "*.tmp"));
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

        public sealed class ThrowingConfig : IConfig
        {
            public string Value => throw new InvalidOperationException("Candidate serialization failed.");
        }

        public sealed class PartiallyThrowingSecureConfig : IConfigSecure
        {
            public string FirstSecret { get; set; } = string.Empty;

            public string SecondSecret { get; set; } = string.Empty;

            public void Encryption()
            {
                FirstSecret = "encrypted-before-failure";
                throw new InvalidOperationException("Encryption failed after a partial mutation.");
            }

            public void Decrypt()
            {
            }
        }

        public sealed class ReentrantSaveConfig : IConfig
        {
            private string _value;

            public ReentrantSaveConfig()
                : this(string.Empty)
            {
            }

            public ReentrantSaveConfig(string value)
            {
                _value = value;
            }

            [Newtonsoft.Json.JsonIgnore]
            public Action? OnNextRead { get; set; }

            public string Value
            {
                get
                {
                    string capturedValue = _value;
                    var onNextRead = OnNextRead;
                    OnNextRead = null;
                    onNextRead?.Invoke();

                    return capturedValue;
                }
                set => _value = value;
            }

            public void SetValue(string value) => _value = value;
        }
    }
}
