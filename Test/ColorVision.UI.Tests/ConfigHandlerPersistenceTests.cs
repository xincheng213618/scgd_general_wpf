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
    }
}
