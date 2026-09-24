using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSettingsQuickAddTests
{
    [Fact]
    public void CancellingCreationLeavesExistingProfilesAndSelectionUntouched()
    {
        using var fixture = new Fixture();
        var vm = fixture.ViewModel;
        var original = vm.SelectedProfile;
        vm.PrepareAddModelDialog();
        vm.IsAddingModel = true;
        vm.NewProfileApiKey = "draft-only-key";
        vm.NewProfileDraft!.Model = "draft-model";

        vm.CancelAddModel();

        Assert.Same(original, vm.SelectedProfile);
        Assert.Single(vm.Profiles);
        Assert.Null(vm.NewProfileDraft);
        Assert.Empty(vm.NewProfileApiKey);
        Assert.False(vm.IsAddingModel);
        Assert.False(vm.HasUnsavedSettings);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public void CustomConnectionMustBeCompleteAndAddsOnlyToWindowDraft()
    {
        using var fixture = new Fixture();
        var vm = fixture.ViewModel;
        vm.PrepareAddModelDialog();
        vm.SelectConnectProviderCommand.Execute(vm.ConnectProviderOptions.Single(x => x.VendorType == CopilotVendorType.Custom));
        vm.NewProfileApiKey = "custom-test-key";
        Assert.False(vm.CanAddProfile);
        Assert.Empty(vm.NewProfileDraft!.BaseUrl);
        Assert.Empty(vm.NewProfileDraft.Model);

        vm.NewProfileDraft.ProviderType = CopilotProviderType.OpenAICompatible;
        vm.NewProfileDraft.BaseUrl = "https://model.example.test/responses";
        vm.NewProfileDraft.Model = "custom-model";
        vm.NewProfileDraft.Name = "My model";
        vm.NewProfileDraft.SupportsImageInput = true;
        Assert.True(vm.CanAddProfile);
        Assert.True(vm.AddQuickProfile(useNow: false));
        vm.CancelAddModel();

        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("https://model.example.test/responses", vm.SelectedProfile!.BaseUrl);
        Assert.Equal("custom-model", vm.SelectedProfile.Model);
        Assert.Equal("custom-test-key", vm.SelectedProfile.ApiKey);
        Assert.True(vm.SelectedProfile.SupportsImageInput);
        Assert.True(vm.HasUnsavedSettings);
        Assert.Single(fixture.Config.Profiles);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public void SwitchingVendorsDropsCredentialsAndUsesTheNewPreset()
    {
        using var fixture = new Fixture();
        var vm = fixture.ViewModel;
        vm.PrepareAddModelDialog();
        vm.NewProfileApiKey = "first-vendor-key";
        vm.NewProfileDraft!.BaseUrl = "https://first.example.test/v1";
        vm.NewProfileDraft.SupportsImageInput = true;

        vm.SelectConnectProviderCommand.Execute(vm.ConnectProviderOptions.Single(x => x.VendorType == CopilotVendorType.OpenAI));

        Assert.Empty(vm.NewProfileApiKey);
        Assert.Empty(vm.NewProfileDraft!.ApiKey);
        Assert.False(vm.NewProfileDraft.SupportsImageInput);
        Assert.Equal(CopilotVendorCatalog.GetDefaultBaseUrl(CopilotVendorType.OpenAI, vm.NewProfileDraft.ProviderType), vm.NewProfileDraft.BaseUrl);
        Assert.False(vm.CanAddProfile);
        Assert.Single(vm.Profiles);
    }

    internal sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CopilotSettings-" + Guid.NewGuid().ToString("N"), "config.json");
        public CopilotConfig Config { get; }
        public CopilotSettingsViewModel ViewModel { get; }

        public Fixture()
        {
            var profile = new CopilotProfileConfig
            {
                Id = "preview-model", Name = "DeepSeek", VendorType = CopilotVendorType.DeepSeek,
                ApiKey = "synthetic-test-key", Model = "deepseek-v4-pro",
            };
            Config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpEnabled = false,
                McpBearerToken = "synthetic-mcp-token",
                Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
            };
            Config.EnsureInitialized();
            var handler = new ConfigHandler { ConfigFilePath = Path };
            handler.Configs[typeof(CopilotConfig)] = Config;
            ViewModel = new CopilotSettingsViewModel(handler, new CopilotChatState { ActiveProfileId = profile.Id });
        }

        public void Dispose() => ViewModel.Dispose();
    }
}
