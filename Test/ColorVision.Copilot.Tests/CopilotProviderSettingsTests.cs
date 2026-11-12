using Newtonsoft.Json;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotProviderSettingsTests
{
    private static CopilotSettingsViewModel CreateSettings(params CopilotProfileConfig[] profiles)
    {
        var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpEnabled = false, McpBearerToken = "test", Profiles = new(profiles) };
        config.EnsureInitialized();
        var handler = new ConfigHandler { ConfigFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json") };
        handler.Configs[typeof(CopilotConfig)] = config;
        return new CopilotSettingsViewModel(handler, new CopilotChatState());
    }

    [Fact]
    public void LegacyConnectionsRemainIndependentAndNewModelsShareOnlyConnectionFields()
    {
        var legacy = CopilotProfileConfig.CreateDefault();
        legacy.ApiKey = "synthetic-key";
        var second = legacy.Clone();
        second.Id = Guid.NewGuid().ToString("N");
        using var settings = CreateSettings(legacy, second);
        Assert.Equal(2, settings.Providers.Count);
        settings.AddProvider(settings.ConnectProviderOptions.First(option => !option.IsLocalCodex && option.VendorType == CopilotVendorType.OpenAI));
        var provider = settings.SelectedProvider!;
        Assert.True(provider.Models.Count > 1);
        Assert.Single(provider.Models, model => model.IsEnabled);
        Assert.All(provider.Models, model => Assert.True(model.SupportsImageInput));
        provider.Models[0].MaxTokens = 4096;
        provider.ApiKey = "new-key";
        provider.BaseUrl = "https://example.test/v1";
        Assert.All(provider.Models, model => Assert.Equal("new-key", model.ApiKey));
        Assert.All(provider.Models, model => Assert.Equal("https://example.test/v1", model.BaseUrl));
        Assert.Equal(4096, provider.Models[0].MaxTokens);
        Assert.Equal("synthetic-key", settings.Profiles[0].ApiKey);
        Assert.Equal(legacy.BaseUrl, settings.Profiles[0].BaseUrl);
    }

    [Fact]
    public void ProviderDisableRoundTripRetainsModelSwitchesAndPreventsSelection()
    {
        using var settings = CreateSettings();
        settings.AddProvider(settings.ConnectProviderOptions.First(option => option.IsLocalCodex));
        var local = settings.SelectedProvider!;
        Assert.True(local.IsLocalCodex);
        Assert.True(local.Connection.IsConfigured);
        local.IsEnabled = false;
        var config = JsonConvert.DeserializeObject<CopilotConfig>(JsonConvert.SerializeObject(new CopilotConfig
        { SchemaVersion = CopilotConfig.CurrentSchemaVersion, Profiles = new(settings.Profiles) }))!;
        config.EnsureInitialized();
        var restored = Assert.Single(config.Profiles);
        Assert.True(restored.IsEnabled);
        Assert.False(restored.ConnectionEnabled);
        Assert.False(restored.IsConfigured);
        Assert.Null(config.GetPreferredDefaultProfile());
        Assert.Null(CopilotChatStateProfileReconciler.Apply(new CopilotChatState { ActiveProfileId = restored.Id }, config, restored.Id));
        restored.ConnectionEnabled = true;
        Assert.True(restored.IsConfigured);
    }

    [Fact]
    public void DeletedLastProviderStaysDeletedAfterInitialization()
    {
        using var settings = CreateSettings(CopilotProfileConfig.CreateDefault());
        settings.DeleteProvider();
        var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, Profiles = new(settings.Profiles) };
        config.EnsureInitialized();
        Assert.Empty(config.Profiles);
        Assert.Null(settings.SelectedProvider);
    }

    [Fact]
    public void SaveAndReloadPreservesGroupedModelsAndEncryptsSharedCredentials()
    {
        var file = Path.Combine(Path.GetTempPath(), "copilot-provider-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpEnabled = false, McpBearerToken = "test" };
            config.EnsureInitialized();
            var handler = new ConfigHandler { ConfigFilePath = file };
            handler.Configs[typeof(CopilotConfig)] = config;
            using var settings = new CopilotSettingsViewModel(handler, new CopilotChatState());
            settings.AddProvider(settings.ConnectProviderOptions.First(option => !option.IsLocalCodex && option.VendorType == CopilotVendorType.OpenAI));
            settings.SelectedProvider!.ApiKey = "synthetic-provider-secret";
            settings.SelectedProvider.Name = "Work account";
            settings.SelectedProvider.Models[1].IsEnabled = true;
            settings.SelectedProvider.Models[0].MaxTokens = 4096;
            Assert.True(settings.Save(), settings.SettingsStatusText);
            Assert.DoesNotContain("synthetic-provider-secret", File.ReadAllText(file));
            var loadedHandler = new ConfigHandler { ConfigFilePath = file };
            loadedHandler.LoadConfigs();
            using var loaded = new CopilotSettingsViewModel(loadedHandler, new CopilotChatState());
            var provider = Assert.Single(loaded.Providers);
            Assert.Equal("Work account", provider.Name);
            Assert.Equal("synthetic-provider-secret", provider.ApiKey);
            Assert.Equal(2, provider.Models.Count(model => model.IsEnabled));
            Assert.Equal(4096, provider.Models[0].MaxTokens);
            Assert.Equal(1050000, provider.Models[0].ContextWindowTokens);
            loaded.DeleteProvider();
            Assert.True(loaded.Save(), loaded.SettingsStatusText);
            loadedHandler.LoadConfigs();
            Assert.Empty(loadedHandler.GetRequiredService<CopilotConfig>().Profiles);
        }
        finally
        {
            File.Delete(file);
            File.Delete(file + ".bak");
        }
    }
}
