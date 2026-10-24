using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotProviderSettingsTests
{
    [Theory]
    [InlineData(nameof(CopilotProfileConfig.Model))]
    [InlineData(nameof(CopilotProfileConfig.BaseUrl))]
    [InlineData(nameof(CopilotProfileConfig.ProviderType))]
    public void ReassigningTheSameModelEndpointPreservesItsImageDeclaration(string property)
    {
        var profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            BaseUrl = "https://example.test/v1",
            Model = "custom-vision-model",
            SupportsImageInput = true,
        };
        var notifications = new List<string?>();
        profile.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        switch (property)
        {
            case nameof(CopilotProfileConfig.Model):
                profile.Model = "  custom-vision-model  ";
                break;
            case nameof(CopilotProfileConfig.BaseUrl):
                profile.BaseUrl = "  https://example.test/v1  ";
                break;
            case nameof(CopilotProfileConfig.ProviderType):
                profile.ProviderType = CopilotProviderType.OpenAICompatible;
                break;
        }

        Assert.True(profile.SupportsImageInput);
        Assert.Empty(notifications);

        switch (property)
        {
            case nameof(CopilotProfileConfig.Model):
                profile.Model = "another-model";
                break;
            case nameof(CopilotProfileConfig.BaseUrl):
                profile.BaseUrl = "https://other.example.test/v1";
                break;
            case nameof(CopilotProfileConfig.ProviderType):
                profile.ProviderType = CopilotProviderType.AnthropicCompatible;
                break;
        }

        Assert.False(profile.SupportsImageInput);
        Assert.Contains(property, notifications);
    }

    [Fact]
    public void SaveAndReloadPreservesCustomProviderIdentityAndManualModelCapabilities()
    {
        var file = Path.Combine(Path.GetTempPath(), "copilot-custom-provider-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpEnabled = false, McpBearerToken = "test" };
            config.EnsureInitialized();
            var handler = new ConfigHandler { ConfigFilePath = file };
            handler.Configs[typeof(CopilotConfig)] = config;
            using var settings = new CopilotSettingsViewModel(handler, new CopilotChatState());
            settings.AddProvider(settings.ConnectProviderOptions.First(option => !option.IsLocalCodex && option.VendorType == CopilotVendorType.Custom));
            settings.SelectedProvider!.BaseUrl = "https://example.test/v1";
            settings.SelectedProvider.ApiKey = "synthetic-custom-key";
            var expectedModels = new[]
            {
                (Id: "gpt-6-astra", Context: 131072, Output: 4096, Vision: false),
                (Id: "gpt-6-astra-unknown", Context: 262144, Output: 16384, Vision: true),
            };
            for (var index = 0; index < expectedModels.Length; index++)
            {
                var expected = expectedModels[index];
                var original = index == 0 ? settings.SelectedProvider.Models[0] : null;
                var editor = new CopilotModelEditor(settings.CreateModelDraft(original), settings.AvailableModelPresets)
                {
                    ModelId = expected.Id,
                    ContextWindowText = expected.Context.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    MaxOutputText = expected.Output.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    SupportsImages = expected.Vision,
                };
                Assert.True(editor.SmartConfiguration);
                Assert.True(editor.Validate(), editor.Error);
                Assert.True(settings.CommitModelDraft(editor.Draft, original, out var error), error);
            }

            Assert.True(settings.Save(), settings.SettingsStatusText);
            Assert.All(settings.SelectedProvider!.Models, model => Assert.Equal(CopilotVendorType.Custom, model.VendorType));
            var loadedHandler = new ConfigHandler { ConfigFilePath = file };
            loadedHandler.LoadConfigs();
            using var loaded = new CopilotSettingsViewModel(loadedHandler, new CopilotChatState());
            var provider = Assert.Single(loaded.Providers);
            Assert.Equal(expectedModels.Length, provider.Models.Count);
            foreach (var expected in expectedModels)
            {
                var profile = Assert.Single(provider.Models, model => model.Model == expected.Id);
                Assert.False(string.IsNullOrWhiteSpace(profile.ConnectionId));
                Assert.Equal(provider.Id, profile.ConnectionId);
                Assert.Equal(CopilotVendorType.Custom, profile.VendorType);
                Assert.Equal(expected.Context, profile.ContextWindowTokens);
                Assert.Equal(expected.Output, profile.MaxTokens);
                Assert.Equal(expected.Vision, profile.SupportsImageInput);
                Assert.True(profile.UseModelDefaults);
                Assert.Null(CopilotModelDefaults.Find(profile));
                var editor = new CopilotModelEditor(loaded.CreateModelDraft(profile), loaded.AvailableModelPresets);
                Assert.True(editor.CanEditCapabilities);
                Assert.True(editor.CanEditContextWindow);
            }
        }
        finally
        {
            File.Delete(file);
            File.Delete(file + ".bak");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void LegacyProfilesWithoutAConnectionStillInferTheirVendor(string connectionId)
    {
        var profile = new CopilotProfileConfig
        {
            ConnectionId = connectionId,
            VendorType = CopilotVendorType.Custom,
            BaseUrl = "https://example.test/v1",
            Model = "gpt-6-astra",
            ContextWindowTokens = 131072,
            MaxTokens = 4096,
            SupportsImageInput = false,
        };

        Assert.True(profile.EnsureValid());
        Assert.Equal(CopilotVendorType.OpenAI, profile.VendorType);
        Assert.Equal(131072, profile.ContextWindowTokens);
        Assert.Equal(4096, profile.MaxTokens);
        Assert.False(profile.SupportsImageInput);
        Assert.False(profile.EnsureValid());
    }
}
