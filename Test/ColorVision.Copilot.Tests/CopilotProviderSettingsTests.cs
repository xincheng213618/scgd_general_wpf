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
    public void ModelEditorUsesAnIsolatedDraftAndValidatesInheritedBudget()
    {
        using var settings = CreateSettings(CopilotProfileConfig.CreateDefault());
        var original = settings.SelectedProfile!;
        var draft = settings.CreateModelDraft(original);
        var editor = new CopilotModelEditor(draft, settings.AvailableModelPresets)
        { ModelId = "custom-model", SmartConfiguration = false, ContextWindowText = "0", MaxOutputText = "128000", SupportsImages = true };
        Assert.Equal("deepseek-v4-pro", original.Model);
        Assert.True(editor.Validate());
        settings.AgentContextWindowTokens = 32768;
        Assert.False(settings.CommitModelDraft(draft, original, out var error));
        Assert.Contains("Agent", error);
        editor.ContextWindowText = "256000";
        Assert.True(editor.Validate());
        Assert.True(settings.CommitModelDraft(draft, original, out _));
        var restored = JsonConvert.DeserializeObject<CopilotProfileConfig>(JsonConvert.SerializeObject(settings.SelectedProfile))!;
        Assert.Equal(128000, restored.MaxTokens);
        Assert.Equal(256000, restored.ContextWindowTokens);
        Assert.True(restored.SupportsImageInput);
        var budget = CopilotAgentRunBudget.Resolve(new CopilotAgentRequest { Profile = restored });
        Assert.Equal(256000, budget.ContextWindowTokens);
        var history = CopilotConversationRequestBuilder.ResolveHistoryLimits(restored.ContextWindowTokens, restored.MaxTokens);
        Assert.Equal((256000 - 128000) / 2 * CopilotTokenEstimator.AsciiCharactersPerToken, history.MaximumCharacters);
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
    public void DefaultsRequireAnExactKnownModelAndLocalOutputLimitIsReadOnly()
    {
        var profile = new CopilotProfileConfig { VendorType = CopilotVendorType.OpenAI, Model = "gpt-6-astra" };
        Assert.True(CopilotModelDefaults.Apply(profile));
        Assert.True(profile.SupportsImageInput);
        Assert.Equal(1050000, profile.ContextWindowTokens);
        profile.Model = "gpt-6-astra-unknown";
        Assert.False(CopilotModelDefaults.Apply(profile));
        Assert.False(profile.SupportsImageInput);
        profile.ProviderType = CopilotProviderType.LocalCodex;
        var editor = new CopilotModelEditor(profile, []);
        Assert.False(editor.CanEditOutputLimit);
        editor.SmartConfiguration = false;
        editor.SupportsImages = true;
        editor.ModelId = "different-manual-model";
        Assert.False(editor.SupportsImages);
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
