using ColorVision.Common.MVVM;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class ConfigReloadCoordinatorTests : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionConfigReload-{Guid.NewGuid():N}");

        public ConfigReloadCoordinatorTests()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        [Fact]
        public void LoadConfigs_BindsParticipantsInOrderAndAggregatesEveryFailure()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "C1");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.LoadConfigs();

            var calls = new List<string>();
            configHandler.ReloadCoordinator.Register(new RecordingParticipant("late", 30, calls));
            configHandler.ReloadCoordinator.Register(new RecordingParticipant("throws", 20, calls, shouldThrow: true));
            configHandler.ReloadCoordinator.Register(new RecordingParticipant("early", 10, calls));
            configHandler.ConfigsReloaded += (_, _) => throw new InvalidOperationException("legacy failure");
            configHandler.ConfigsReloaded += (_, _) => calls.Add("legacy:C2");

            WriteConfig(configFilePath, "C2");
            ConfigReloadResult result = configHandler.LoadConfigsWithResult();

            Assert.Equal(
                ["early:C2", "throws:C2", "late:C2", "legacy:C2"],
                calls);
            Assert.False(result.Succeeded);
            Assert.Equal(3, result.AttemptedParticipantCount);
            Assert.Equal(2, result.AttemptedLegacySubscriberCount);
            Assert.Collection(
                result.Failures,
                failure =>
                {
                    Assert.Equal(ConfigReloadFailureKind.Participant, failure.Kind);
                    Assert.Equal("throws", failure.OwnerName);
                },
                failure => Assert.Equal(ConfigReloadFailureKind.LegacySubscriber, failure.Kind));
            Assert.Equal(2, result.CreateAggregateException().InnerExceptions.Count);
            Assert.Same(result, configHandler.LastReloadResult);
        }

        [Fact]
        public void LoadConfigs_OwnerUnsubscribesC1AndBindsC2BeforeReturning()
        {
            string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
            WriteConfig(configFilePath, "C1");
            var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
            configHandler.LoadConfigs();
            var owner = new ObservableConfigOwner();
            configHandler.ReloadCoordinator.RegisterAndBind(owner);
            ObservableReloadConfig c1 = owner.CurrentConfig!;

            WriteConfig(configFilePath, "C2");
            ConfigReloadResult result = configHandler.LoadConfigsWithResult();
            ObservableReloadConfig c2 = owner.CurrentConfig!;

            Assert.True(result.Succeeded);
            Assert.NotSame(c1, c2);
            Assert.Equal("C2", c2.Value);

            c1.Value = "stale-event";
            Assert.Equal(0, owner.CurrentConfigChangeCount);
            c2.Value = "C2-event";
            Assert.Equal(1, owner.CurrentConfigChangeCount);
        }

        [Fact]
        public void RegisterAndBind_BindsOnlyTheNewOwner()
        {
            var configHandler = new ConfigHandler();
            var calls = new List<string>();
            var existing = new RecordingParticipant("existing", 10, calls);
            var later = new RecordingParticipant("later", 20, calls);

            configHandler.ReloadCoordinator.RegisterAndBind(existing);
            calls.Clear();

            ConfigReloadResult result = configHandler.ReloadCoordinator.RegisterAndBind(later);

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.AttemptedParticipantCount);
            Assert.Equal(["later:"], calls);
        }

        [Fact]
        public void ConfigHandlerRegistrationEntryBindsOnlyNewReferences()
        {
            var configHandler = new ConfigHandler();
            var calls = new List<string>();
            var first = new RecordingParticipant("first", 10, calls);
            var second = new RecordingParticipant("second", 20, calls);

            ConfigReloadResult firstResult = configHandler.RegisterReloadParticipants(first, first);
            ConfigReloadResult secondResult = configHandler.RegisterReloadParticipants(first, second, second);
            ConfigReloadResult repeatedResult = configHandler.RegisterReloadParticipants(first, second);

            Assert.True(firstResult.Succeeded);
            Assert.True(secondResult.Succeeded);
            Assert.True(repeatedResult.Succeeded);
            Assert.Equal(1, firstResult.AttemptedParticipantCount);
            Assert.Equal(1, secondResult.AttemptedParticipantCount);
            Assert.Equal(0, repeatedResult.AttemptedParticipantCount);
            Assert.Equal(["first:", "second:"], calls);
        }

        private static void WriteConfig(string fileName, string value)
        {
            var json = new JObject
            {
                [nameof(ObservableReloadConfig)] = JObject.FromObject(new ObservableReloadConfig { Value = value }),
            };
            File.WriteAllText(fileName, json.ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }

        private sealed class RecordingParticipant : IConfigReloadParticipant
        {
            private readonly List<string> _calls;
            private readonly bool _shouldThrow;

            public RecordingParticipant(string name, int order, List<string> calls, bool shouldThrow = false)
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
                string value = currentConfig.GetRequiredService<ObservableReloadConfig>().Value;
                _calls.Add($"{ConfigReloadName}:{value}");
                if (_shouldThrow)
                    throw new InvalidOperationException("participant failure");
            }
        }

        private sealed class ObservableConfigOwner : IConfigReloadParticipant
        {
            public string ConfigReloadName => nameof(ObservableConfigOwner);

            public int ConfigReloadOrder => 0;

            public ObservableReloadConfig? CurrentConfig { get; private set; }

            public int CurrentConfigChangeCount { get; private set; }

            public void BindCurrentConfig(IConfigService currentConfig)
            {
                ObservableReloadConfig config = currentConfig.GetRequiredService<ObservableReloadConfig>();
                if (ReferenceEquals(CurrentConfig, config))
                    return;

                if (CurrentConfig != null)
                    CurrentConfig.PropertyChanged -= CurrentConfig_PropertyChanged;
                CurrentConfig = config;
                CurrentConfig.PropertyChanged += CurrentConfig_PropertyChanged;
            }

            private void CurrentConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                CurrentConfigChangeCount++;
            }
        }

        public sealed class ObservableReloadConfig : ViewModelBase, IConfig
        {
            public string Value
            {
                get => _value;
                set => SetProperty(ref _value, value ?? string.Empty);
            }
            private string _value = string.Empty;
        }
    }
}
