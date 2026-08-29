using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBackendSyncTransactionTests : IDisposable
{
    private const string SyncBaseUrl = "https://sync.example.test/configuration";
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ColorVisionCopilotBackendSync-{Guid.NewGuid():N}");

    public CopilotBackendSyncTransactionTests()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task PersistenceFailureLeavesViewModelRuntimeConfigAndDiskUnchanged()
    {
        string configFilePath = CreateExistingConfigFile();
        byte[] originalBytes = File.ReadAllBytes(configFilePath);
        var localProfile = CreateLocalProfile("local-profile", "Saved local profile");
        var liveConfig = CreateLiveConfig(localProfile);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        var originalViewModelProfile = Assert.Single(viewModel.Profiles);
        var originalRuntimeProfile = Assert.Single(liveConfig.Profiles);
        var originalSelectedProfile = viewModel.SelectedProfile;
        string originalActiveProfileId = viewModel.ActiveProfileId;

        using (new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await viewModel.SyncBackendConfigAsync();
        }

        Assert.StartsWith("Sync failed:", viewModel.BackendSyncStatusText, StringComparison.Ordinal);
        Assert.Contains("were not saved", viewModel.BackendSyncStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.False(viewModel.HasUnsavedSettings);
        Assert.Same(originalSelectedProfile, viewModel.SelectedProfile);
        Assert.Equal(originalActiveProfileId, viewModel.ActiveProfileId);
        Assert.Same(originalViewModelProfile, Assert.Single(viewModel.Profiles));
        Assert.Same(originalRuntimeProfile, Assert.Single(liveConfig.Profiles));
        Assert.Equal("Saved local profile", originalRuntimeProfile.Name);
        Assert.Equal("local-secret", originalRuntimeProfile.ApiKey);
        Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task SuccessfulSyncPersistsBackendProfilesWithoutStealingLocalDrafts()
    {
        string configFilePath = CreateExistingConfigFile();
        var localProfile = CreateLocalProfile("local-profile", "Saved local profile");
        var liveConfig = CreateLiveConfig(localProfile);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        viewModel.SelectedProfile!.Name = "Unsaved local draft";
        var propertyChanges = new List<string?>();
        viewModel.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);

        await viewModel.SyncBackendConfigAsync();

        Assert.Contains("saved", viewModel.BackendSyncStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Other settings still have unsaved changes", viewModel.SettingsStatusText, StringComparison.Ordinal);
        Assert.True(viewModel.HasAppliedChanges);
        Assert.True(viewModel.HasUnsavedSettings);
        Assert.Equal("Unsaved local draft", viewModel.Profiles.Single(profile => profile.Id == "local-profile").Name);
        Assert.Equal("Saved local profile", liveConfig.Profiles.Single(profile => profile.Id == "local-profile").Name);

        var runtimeManagedProfile = liveConfig.Profiles.Single(profile => profile.SyncProfileId == "managed-default");
        var displayManagedProfile = viewModel.Profiles.Single(profile => profile.SyncProfileId == "managed-default");
        Assert.Equal(runtimeManagedProfile.Id, displayManagedProfile.Id);
        Assert.Equal(runtimeManagedProfile.Id, viewModel.ActiveProfileId);
        Assert.Same(displayManagedProfile, viewModel.SelectedProfile);
        Assert.True(viewModel.IsSelectedProfileActiveInChat);
        Assert.Contains(nameof(CopilotSettingsViewModel.ActiveProfileId), propertyChanges);

        JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
        Assert.Equal("preserved", persisted["UnrelatedSection"]!["Value"]);
        JArray persistedProfiles = Assert.IsType<JArray>(persisted[nameof(CopilotConfig)]![nameof(CopilotConfig.Profiles)]);
        JObject persistedLocalProfile = persistedProfiles
            .Children<JObject>()
            .Single(profile => (string?)profile[nameof(CopilotProfileConfig.Id)] == "local-profile");
        JObject persistedManagedProfile = persistedProfiles
            .Children<JObject>()
            .Single(profile => (string?)profile[nameof(CopilotProfileConfig.SyncProfileId)] == "managed-default");
        Assert.Equal("Saved local profile", (string?)persistedLocalProfile[nameof(CopilotProfileConfig.Name)]);
        Assert.True(CopilotCredentialProtector.IsProtected(
            (string?)persistedManagedProfile[nameof(CopilotProfileConfig.ApiKey)]));
        Assert.Equal("managed-secret", runtimeManagedProfile.ApiKey);
        Assert.Equal("managed-secret", displayManagedProfile.ApiKey);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void SettingsSaveFailureKeepsRuntimeConfigAndDraftStateRetryable()
    {
        string configFilePath = CreateExistingConfigFile();
        byte[] originalBytes = File.ReadAllBytes(configFilePath);
        var liveConfig = CreateLiveConfig(CreateLocalProfile("local-profile", "Saved local profile"));
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        viewModel.SelectedProfile!.Name = "Unsaved local draft";

        bool saved;
        using (new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            saved = viewModel.Save();
        }

        Assert.False(saved);
        Assert.StartsWith("Settings were not saved.", viewModel.SettingsStatusText, StringComparison.Ordinal);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.True(viewModel.HasUnsavedSettings);
        Assert.Equal("Unsaved local draft", viewModel.SelectedProfile.Name);
        Assert.Equal("Saved local profile", Assert.Single(liveConfig.Profiles).Name);
        Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task FutureSchemaSyncFailureDoesNotPublishOrOverwriteProfiles()
    {
        string configFilePath = CreateExistingConfigFile();
        byte[] originalBytes = File.ReadAllBytes(configFilePath);
        var originalProfile = CreateLocalProfile("future-local", "Future local profile");
        var liveConfig = CreateLiveConfig(originalProfile);
        liveConfig.SchemaVersion = CopilotConfig.CurrentSchemaVersion + 1;
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        var originalViewModelProfile = Assert.Single(viewModel.Profiles);

        await viewModel.SyncBackendConfigAsync();

        Assert.StartsWith("Sync failed:", viewModel.BackendSyncStatusText, StringComparison.Ordinal);
        Assert.Contains("newer application version", viewModel.BackendSyncStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.HasAppliedChanges);
        Assert.Same(originalViewModelProfile, Assert.Single(viewModel.Profiles));
        Assert.Same(originalProfile, Assert.Single(liveConfig.Profiles));
        Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void TrySaveRestoresCandidateSecretsAfterFailureAndSuccess()
    {
        string configFilePath = CreateExistingConfigFile();
        var candidate = CreateLiveConfig(CreateLocalProfile("local-profile", "Local profile"));
        var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };

        bool savedWhileLocked;
        using (new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            savedWhileLocked = configHandler.TrySave(candidate, out _);
        }

        Assert.False(savedWhileLocked);
        Assert.Equal("local-secret", Assert.Single(candidate.Profiles).ApiKey);
        Assert.Equal("test-mcp-token", candidate.McpBearerToken);

        Assert.True(configHandler.TrySave(candidate, out string errorMessage), errorMessage);
        Assert.Equal("local-secret", Assert.Single(candidate.Profiles).ApiKey);
        Assert.Equal("test-mcp-token", candidate.McpBearerToken);
        JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
        JToken persistedConfig = persisted[nameof(CopilotConfig)]!;
        Assert.True(CopilotCredentialProtector.IsProtected(
            (string?)persistedConfig[nameof(CopilotConfig.McpBearerToken)]));
        Assert.True(CopilotCredentialProtector.IsProtected(
            (string?)persistedConfig[nameof(CopilotConfig.Profiles)]![0]![nameof(CopilotProfileConfig.ApiKey)]));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task SyncWithoutBackendDefaultNeverActivatesAnUnsavedDraft()
    {
        string configFilePath = CreateExistingConfigFile();
        var savedProfile = CreateLocalProfile("saved-profile", "Saved profile");
        var liveConfig = CreateLiveConfig(savedProfile);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateEmptyBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        var unsavedDraft = CreateLocalProfile("unsaved-draft", "Unsaved draft");
        viewModel.Profiles.Add(unsavedDraft);
        viewModel.SelectedProfile = unsavedDraft;

        await viewModel.SyncBackendConfigAsync();

        Assert.Equal("unsaved-draft", viewModel.SelectedProfile.Id);
        Assert.NotEqual("unsaved-draft", viewModel.ActiveProfileId);
        Assert.NotNull(liveConfig.FindProfile(viewModel.ActiveProfileId));
        Assert.Null(liveConfig.FindProfile("unsaved-draft"));
        Assert.True(viewModel.HasUnsavedSettings);
    }

    [Fact]
    public void DuplicatingManagedProfileCreatesAnIndependentLocalDraft()
    {
        string configFilePath = CreateExistingConfigFile();
        var managedProfile = CreateManagedProfile("managed-local", "managed-remote", "Managed profile");
        var liveConfig = CreateLiveConfig(managedProfile);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateEmptyBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);

        viewModel.DuplicateProfileCommand.Execute(null);

        Assert.Equal(2, viewModel.Profiles.Count);
        var duplicate = Assert.IsType<CopilotProfileConfig>(viewModel.SelectedProfile);
        Assert.NotEqual(managedProfile.Id, duplicate.Id);
        Assert.False(duplicate.IsBackendSynced);
        Assert.Equal(string.Empty, duplicate.SyncSource);
        Assert.Equal(string.Empty, duplicate.SyncProfileId);
        Assert.True(viewModel.HasUnsavedSettings);
    }

    [Fact]
    public async Task EmptyBackendSnapshotDoesNotResurrectADeletedLocalDraft()
    {
        string configFilePath = CreateExistingConfigFile();
        var localProfile = CreateLocalProfile("deleted-local", "Deleted local profile");
        var managedProfile = CreateManagedProfile("managed-local", "managed-remote", "Managed profile");
        var liveConfig = CreateLiveConfig(localProfile, managedProfile);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateEmptyBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);

        viewModel.DeleteProfileCommand.Execute(null);
        Assert.DoesNotContain(viewModel.Profiles, profile => profile.Id == localProfile.Id);

        await viewModel.SyncBackendConfigAsync();

        Assert.True(viewModel.HasUnsavedSettings);
        Assert.DoesNotContain(viewModel.Profiles, profile => profile.Id == localProfile.Id);
        Assert.DoesNotContain(viewModel.Profiles, profile => profile.Id == managedProfile.Id);
        Assert.Single(viewModel.Profiles);
        Assert.Equal(localProfile.Id, viewModel.ActiveProfileId);
        Assert.Equal(localProfile.Id, Assert.Single(liveConfig.Profiles).Id);

        JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
        JArray persistedProfiles = Assert.IsType<JArray>(
            persisted[nameof(CopilotConfig)]![nameof(CopilotConfig.Profiles)]);
        Assert.Equal(localProfile.Id, (string?)Assert.Single(persistedProfiles)[nameof(CopilotProfileConfig.Id)]);
    }

    [Fact]
    public void SuccessfulSaveReconcilesViewModelWithNormalizedRuntimeProfiles()
    {
        string configFilePath = CreateExistingConfigFile();
        var managedProfile = CreateManagedProfile("managed-local", "managed-remote", "Managed profile");
        var liveConfig = CreateLiveConfig(managedProfile);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateEmptyBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        viewModel.SelectedProfile!.BaseUrl = "http://public.example.test/v1";

        bool saved = viewModel.Save();

        Assert.True(saved, viewModel.SettingsStatusText);
        Assert.DoesNotContain(liveConfig.Profiles, profile => profile.Id == managedProfile.Id);
        Assert.DoesNotContain(viewModel.Profiles, profile => profile.Id == managedProfile.Id);
        Assert.Equal(
            liveConfig.Profiles.Select(profile => profile.Id),
            viewModel.Profiles.Select(profile => profile.Id));
        Assert.NotNull(viewModel.SelectedProfile);
        Assert.NotNull(liveConfig.FindProfile(viewModel.SelectedProfile.Id));
        Assert.NotNull(liveConfig.FindProfile(viewModel.ActiveProfileId));
        Assert.False(viewModel.HasUnsavedSettings);

        JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
        JArray persistedProfiles = Assert.IsType<JArray>(
            persisted[nameof(CopilotConfig)]![nameof(CopilotConfig.Profiles)]);
        Assert.Equal(
            liveConfig.Profiles.Select(profile => profile.Id),
            persistedProfiles.Select(profile => (string?)profile[nameof(CopilotProfileConfig.Id)]));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task NotificationFailureIsReportedAfterTheProfilesWerePersisted()
    {
        string configFilePath = CreateExistingConfigFile();
        var liveConfig = CreateLiveConfig(CreateLocalProfile("local-profile", "Local profile"));
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        liveConfig.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CopilotConfig.Profiles))
                throw new InvalidOperationException("Profile notification failed.");
        };

        await viewModel.SyncBackendConfigAsync();

        Assert.StartsWith("Revision revision-atomic was saved", viewModel.BackendSyncStatusText, StringComparison.Ordinal);
        Assert.Contains("could not refresh", viewModel.BackendSyncStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.HasAppliedChanges);
        Assert.Contains(liveConfig.Profiles, profile => profile.SyncProfileId == "managed-default");
        Assert.DoesNotContain(viewModel.Profiles, profile => profile.SyncProfileId == "managed-default");

        JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
        JArray persistedProfiles = Assert.IsType<JArray>(
            persisted[nameof(CopilotConfig)]![nameof(CopilotConfig.Profiles)]);
        Assert.Contains(
            persistedProfiles.Children<JObject>(),
            profile => (string?)profile[nameof(CopilotProfileConfig.SyncProfileId)] == "managed-default");
    }

    [Fact]
    public async Task SyncRemovesDuplicateManagedIdentityAndUsesBackendOrder()
    {
        string configFilePath = CreateExistingConfigFile();
        var managedA = CreateManagedProfile("managed-a", "remote-a", "Managed A");
        var duplicateA = CreateManagedProfile("managed-a-duplicate", "remote-a", "Managed A duplicate");
        var managedB = CreateManagedProfile("managed-b", "remote-b", "Managed B");
        var liveConfig = CreateLiveConfig(managedA, duplicateA, managedB);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        var response = new CopilotBackendConfigResponse
        {
            SchemaVersion = 1,
            Revision = "revision-reordered",
            Profiles =
            [
                CreateBackendProfile("remote-b", "Managed B updated"),
                CreateBackendProfile("remote-a", "Managed A updated"),
            ],
        };
        using var httpClient = CreateBackendHttpClient(response);
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);

        await viewModel.SyncBackendConfigAsync();

        Assert.Equal(
            ["remote-b", "remote-a"],
            liveConfig.Profiles.Select(profile => profile.SyncProfileId));
        Assert.Equal(
            ["remote-b", "remote-a"],
            viewModel.Profiles.Select(profile => profile.SyncProfileId));
        Assert.Equal("managed-b", liveConfig.Profiles[0].Id);
        Assert.Equal("managed-a", liveConfig.Profiles[1].Id);
        Assert.DoesNotContain(liveConfig.Profiles, profile => profile.Id == duplicateA.Id);
        Assert.Contains("1 removed", viewModel.BackendSyncStatusText, StringComparison.Ordinal);

        JObject persisted = JObject.Parse(File.ReadAllText(configFilePath));
        JArray persistedProfiles = Assert.IsType<JArray>(
            persisted[nameof(CopilotConfig)]![nameof(CopilotConfig.Profiles)]);
        Assert.Equal(
            ["remote-b", "remote-a"],
            persistedProfiles.Select(profile => (string?)profile[nameof(CopilotProfileConfig.SyncProfileId)]));
    }

    [Fact]
    public async Task SyncClearsDirtyStateWhenOnlyManagedDraftWasOverwritten()
    {
        string configFilePath = CreateExistingConfigFile();
        var managedProfile = CreateManagedProfile("managed-local", "managed-default", "Managed original");
        var liveConfig = CreateLiveConfig(managedProfile);
        var configHandler = CreateConfigHandler(configFilePath, liveConfig);
        using var httpClient = CreateBackendHttpClient(CreateBackendResponse());
        using var viewModel = CreateViewModel(configHandler, liveConfig, httpClient);
        viewModel.SelectedProfile!.Name = "Unsaved managed draft";
        Assert.True(viewModel.HasUnsavedSettings);

        await viewModel.SyncBackendConfigAsync();

        Assert.False(viewModel.HasUnsavedSettings);
        Assert.Equal("Managed default", viewModel.SelectedProfile!.Name);
        Assert.Equal("Managed default", Assert.Single(liveConfig.Profiles).Name);
        Assert.DoesNotContain("Other settings still have unsaved changes", viewModel.SettingsStatusText, StringComparison.Ordinal);
    }

    private static CopilotSettingsViewModel CreateViewModel(
        ConfigHandler configHandler,
        CopilotConfig liveConfig,
        HttpClient httpClient)
    {
        var client = new CopilotBackendSyncClient(
            httpClient,
            () => new CopilotBackendDeviceIdentity(
                "ColorVision",
                "1.0.0",
                "test-device",
                "10.0",
                "X64",
                "test-version-key"));
        return new CopilotSettingsViewModel(
            configHandler,
            client,
            new CopilotChatState { ActiveProfileId = liveConfig.Profiles[0].Id });
    }

    private static ConfigHandler CreateConfigHandler(string configFilePath, CopilotConfig liveConfig)
    {
        var configHandler = new ConfigHandler { ConfigFilePath = configFilePath };
        configHandler.Configs[typeof(CopilotConfig)] = liveConfig;
        return configHandler;
    }

    private static CopilotConfig CreateLiveConfig(params CopilotProfileConfig[] profiles)
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            BackendSyncUrl = SyncBaseUrl,
            McpBearerToken = "test-mcp-token",
            Profiles = new ObservableCollection<CopilotProfileConfig>(profiles),
        };
        config.EnsureInitialized();
        return config;
    }

    private static CopilotProfileConfig CreateLocalProfile(string id, string name)
    {
        var profile = new CopilotProfileConfig
        {
            Id = id,
            Name = name,
            VendorType = CopilotVendorType.DeepSeek,
            ProviderType = CopilotProviderType.AnthropicCompatible,
            ApiKey = "local-secret",
            BaseUrl = "https://api.deepseek.com/anthropic",
            Model = "deepseek-test",
            ReasoningMode = CopilotReasoningMode.Disabled,
        };
        profile.EnsureValid();
        return profile;
    }

    private static CopilotProfileConfig CreateManagedProfile(string id, string remoteId, string name)
    {
        var profile = CreateLocalProfile(id, name);
        profile.SyncSource = "https://sync.example.test";
        profile.SyncProfileId = remoteId;
        return profile;
    }

    private static CopilotBackendConfigResponse CreateEmptyBackendResponse()
    {
        return new CopilotBackendConfigResponse
        {
            SchemaVersion = 1,
            Revision = "revision-empty",
            DefaultProfileId = null,
            Profiles = [],
        };
    }

    private static CopilotBackendConfigResponse CreateBackendResponse()
    {
        return new CopilotBackendConfigResponse
        {
            SchemaVersion = 1,
            Revision = "revision-atomic",
            DefaultProfileId = "managed-default",
            Profiles =
            [
                new CopilotBackendProfile
                {
                    Id = "managed-default",
                    Name = "Managed default",
                    VendorType = nameof(CopilotVendorType.DeepSeek),
                    ProviderType = nameof(CopilotProviderType.AnthropicCompatible),
                    BaseUrl = "https://api.deepseek.com/anthropic",
                    Model = "deepseek-managed",
                    ApiKey = "managed-secret",
                    ReasoningMode = nameof(CopilotReasoningMode.Disabled),
                },
            ],
        };
    }

    private static CopilotBackendProfile CreateBackendProfile(string id, string name)
    {
        return new CopilotBackendProfile
        {
            Id = id,
            Name = name,
            VendorType = nameof(CopilotVendorType.DeepSeek),
            ProviderType = nameof(CopilotProviderType.AnthropicCompatible),
            BaseUrl = "https://api.deepseek.com/anthropic",
            Model = $"deepseek-{id}",
            ApiKey = $"secret-{id}",
            ReasoningMode = nameof(CopilotReasoningMode.Disabled),
        };
    }

    private static HttpClient CreateBackendHttpClient(CopilotBackendConfigResponse response)
    {
        string json = JsonSerializer.Serialize(response);
        return new HttpClient(new StaticJsonHandler(json));
    }

    private string CreateExistingConfigFile()
    {
        string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
        File.WriteAllText(
            configFilePath,
            new JObject
            {
                ["UnrelatedSection"] = new JObject { ["Value"] = "preserved" },
            }.ToString());
        return configFilePath;
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

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }
}
