using ColorVision.Copilot;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBackendSyncSecurityTests
{
    [Fact]
    public void FreshConfigurationDoesNotPreauthorizeAPublicHttpBackend()
    {
        var config = new CopilotConfig();

        Assert.True(config.EnsureInitialized());

        Assert.Equal(CopilotConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Equal(string.Empty, config.BackendSyncUrl);
        Assert.False(config.AllowInsecureBackendSync);
    }

    [Fact]
    public void RetiredInsecureBackendOverrideIsNotPersisted()
    {
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            AllowInsecureBackendSync = true,
        };

        var json = JsonConvert.SerializeObject(config);

        Assert.DoesNotContain(nameof(CopilotConfig.AllowInsecureBackendSync), json, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaSevenMigrationRemovesProfilesDownloadedFromTheLegacyHttpBackend()
    {
        var profile = new CopilotProfileConfig
        {
            Id = "legacy-profile",
            Name = "Legacy managed profile",
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "possibly-exposed-key",
            BaseUrl = "https://attacker.example/v1",
            AllowInsecureHttp = true,
            Model = "gpt-test",
            SyncSource = CopilotConfig.LegacyInsecureBackendSyncUrl,
            SyncProfileId = "managed-1",
        };
        var config = new CopilotConfig
        {
            SchemaVersion = 7,
            BackendSyncUrl = "HTTP://XC213618.DDNS.ME.:9998/legacy/path",
            AllowInsecureBackendSync = true,
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };

        Assert.True(config.EnsureInitialized());

        Assert.Equal(CopilotConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Equal(string.Empty, config.BackendSyncUrl);
        Assert.False(config.AllowInsecureBackendSync);
        Assert.DoesNotContain(config.Profiles, item => item.Id == profile.Id);
        Assert.DoesNotContain(config.Profiles, item => item.IsBackendSynced);
        Assert.NotNull(config.GetPreferredDefaultProfile());
        Assert.False(config.EnsureInitialized());
    }

    [Theory]
    [InlineData("https://sync.example.com/root")]
    [InlineData("http://localhost:5050/root")]
    [InlineData("http://127.0.0.1:5050")]
    public void SchemaSevenMigrationPreservesSecureOrLoopbackBackendUrl(string backendUrl)
    {
        var profile = CopilotProfileConfig.CreateDefault();
        var config = new CopilotConfig
        {
            SchemaVersion = 7,
            BackendSyncUrl = backendUrl,
            AllowInsecureBackendSync = true,
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };

        Assert.True(config.EnsureInitialized());

        Assert.Equal(CopilotConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Equal(backendUrl, config.BackendSyncUrl);
        Assert.False(config.AllowInsecureBackendSync);
        Assert.Same(profile, Assert.Single(config.Profiles));
    }

    [Fact]
    public void FutureSchemaConfigurationIsNotMutatedBySecurityMigration()
    {
        var profile = CreateLocalProfile(
            "future-profile",
            CopilotConfig.LegacyInsecureBackendSyncUrl,
            "managed-1");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion + 1,
            BackendSyncUrl = CopilotConfig.LegacyInsecureBackendSyncUrl,
            AllowInsecureBackendSync = true,
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };

        Assert.False(config.EnsureInitialized());

        Assert.Equal(CopilotConfig.LegacyInsecureBackendSyncUrl, config.BackendSyncUrl);
        Assert.True(config.AllowInsecureBackendSync);
        Assert.Same(profile, Assert.Single(config.Profiles));
    }

    [Fact]
    public void InitializationDoesNotRemoveAnExplicitLocalProfile()
    {
        var profile = CopilotProfileConfig.CreateDefault();
        profile.Id = "local-profile";
        profile.ApiKey = "local-key";
        profile.BaseUrl = "http://models.example.com/v1";
        profile.AllowInsecureHttp = true;
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };

        config.EnsureInitialized();

        Assert.Same(profile, Assert.Single(config.Profiles));
        Assert.True(profile.AllowInsecureHttp);
        Assert.True(profile.IsConfigured);
    }

    [Theory]
    [InlineData("http://custom.example.com", "managed-1")]
    [InlineData("not-a-url", "managed-1")]
    [InlineData("https://sync.example.com", "")]
    [InlineData("", "managed-1")]
    public void InitializationRemovesProfilesWithUntrustedOrIncompleteBackendProvenance(
        string syncSource,
        string syncProfileId)
    {
        var profile = CreateLocalProfile("suspect", syncSource, syncProfileId);
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };

        Assert.True(config.EnsureInitialized());

        Assert.DoesNotContain(config.Profiles, item => item.Id == profile.Id);
        Assert.NotNull(config.GetPreferredDefaultProfile());
    }

    [Theory]
    [InlineData("https://sync.example.com")]
    [InlineData("http://localhost:5050")]
    [InlineData("http://127.0.0.1:5050")]
    public void InitializationPreservesProfilesFromTrustedBackendOrigins(string syncSource)
    {
        var profile = CreateLocalProfile("trusted", syncSource, "managed-1");
        profile.AllowInsecureHttp = true;
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };

        Assert.True(config.EnsureInitialized());

        Assert.Same(profile, Assert.Single(config.Profiles));
        Assert.False(profile.AllowInsecureHttp);
        Assert.True(profile.IsConfigured);
    }

    [Fact]
    public void InitializationRemovesTrustedSourceProfileWithUnsafeProviderEndpoint()
    {
        var profile = CreateLocalProfile(
            "unsafe-provider",
            "https://sync.example.com",
            "managed-1");
        profile.BaseUrl = "http://models.example.com/v1";
        profile.AllowInsecureHttp = true;
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };

        Assert.True(config.EnsureInitialized());

        Assert.DoesNotContain(config.Profiles, item => item.Id == profile.Id);
    }

    [Theory]
    [InlineData("http://sync.example.com")]
    [InlineData("http://10.20.30.40:8080/root")]
    public void RemoteHttpBackendIsAlwaysRejected(string baseUrl)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotBackendSyncClient.BuildEndpoint(baseUrl));

        Assert.Contains("requires HTTPS", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://localhost:5050/root", "http://localhost:5050/api/copilot/config")]
    [InlineData("http://localhost.:5050/root", "http://localhost:5050/api/copilot/config")]
    [InlineData("http://127.0.0.1:5050", "http://127.0.0.1:5050/api/copilot/config")]
    [InlineData("http://[::1]:5050", "http://[::1]:5050/api/copilot/config")]
    public void LoopbackHttpBackendRemainsAvailableForLocalDevelopment(
        string baseUrl,
        string expectedEndpoint)
    {
        var endpoint = CopilotBackendSyncClient.BuildEndpoint(baseUrl);

        Assert.Equal(expectedEndpoint, endpoint.AbsoluteUri);
    }

    [Fact]
    public void HttpsBackendUsesTheFixedConfigurationPath()
    {
        var endpoint = CopilotBackendSyncClient.BuildEndpoint(
            "https://sync.example.com:8443/untrusted/path/");

        Assert.Equal(
            "https://sync.example.com:8443/api/copilot/config",
            endpoint.AbsoluteUri);
    }

    [Fact]
    public async Task RejectedRemoteHttpDoesNotReadIdentityOrInvokeTheTransport()
    {
        var handler = new CountingHandler();
        using var httpClient = new HttpClient(handler);
        var identityCalls = 0;
        var client = new CopilotBackendSyncClient(
            httpClient,
            () =>
            {
                identityCalls++;
                return new CopilotBackendDeviceIdentity(
                    "ColorVision",
                    "1.0",
                    "device",
                    "Windows",
                    "X64",
                    "version-key");
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.FetchAsync("http://sync.example.com", CancellationToken.None));

        Assert.Equal(0, identityCalls);
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public void LoopbackTransportDisablesSystemProxy()
    {
        using var handler = CopilotBackendSyncClient.CreateTransportHandler(useProxy: false);

        Assert.False(handler.UseProxy);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
    }

    [Fact]
    public async Task LoopbackFetchUsesTheDedicatedDirectTransportChannel()
    {
        var secureHandler = new CountingHandler();
        var loopbackHandler = new CountingHandler();
        using var secureClient = new HttpClient(secureHandler);
        using var loopbackClient = new HttpClient(loopbackHandler);
        var client = new CopilotBackendSyncClient(
            secureClient,
            loopbackClient,
            CreateIdentity);

        var response = await client.FetchAsync(
            "http://localhost:5050",
            CancellationToken.None);

        Assert.Equal(1, response.SchemaVersion);
        Assert.Equal(0, secureHandler.SendCount);
        Assert.Equal(1, loopbackHandler.SendCount);
    }

    [Fact]
    public void RemoteResponseCannotEnableInsecureProviderHttp()
    {
        var profiles = new ObservableCollection<CopilotProfileConfig>();
        var response = CreateResponse(CreateRemoteProfile(
            "managed-1",
            "http://models.example.com/v1",
            allowInsecureHttp: true));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotBackendSyncClient.MergeProfiles(
                profiles,
                response,
                "https://sync.example.com"));

        Assert.Contains("invalid endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(profiles);
    }

    [Fact]
    public void NullRemoteProfileEntryIsRejectedBeforeMutation()
    {
        var existing = CopilotProfileConfig.CreateDefault();
        var profiles = new ObservableCollection<CopilotProfileConfig> { existing };
        var response = new CopilotBackendConfigResponse
        {
            SchemaVersion = 1,
            Profiles = new List<CopilotBackendProfile> { null! },
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotBackendSyncClient.MergeProfiles(
                profiles,
                response,
                "https://sync.example.com"));

        Assert.Contains("null profile", exception.Message, StringComparison.Ordinal);
        Assert.Same(existing, Assert.Single(profiles));
    }

    [Fact]
    public void SecureImportedProfileIgnoresRemoteInsecureOverride()
    {
        var profiles = new ObservableCollection<CopilotProfileConfig>();
        var response = CreateResponse(CreateRemoteProfile(
            "managed-1",
            "https://models.example.com/v1",
            allowInsecureHttp: true));

        var result = CopilotBackendSyncClient.MergeProfiles(
            profiles,
            response,
            "https://sync.example.com");

        Assert.Equal(1, result.Added);
        var profile = Assert.Single(profiles);
        Assert.False(profile.AllowInsecureHttp);
        Assert.True(profile.IsConfigured);
        Assert.Equal("https://sync.example.com", profile.SyncSource);
    }

    [Fact]
    public void ImportedLoopbackProviderAlsoIgnoresRemoteInsecureOverride()
    {
        var profiles = new ObservableCollection<CopilotProfileConfig>();
        var response = CreateResponse(CreateRemoteProfile(
            "managed-1",
            "http://127.0.0.1:11434/v1",
            allowInsecureHttp: true));

        CopilotBackendSyncClient.MergeProfiles(
            profiles,
            response,
            "https://sync.example.com");

        var profile = Assert.Single(profiles);
        Assert.False(profile.AllowInsecureHttp);
        Assert.True(profile.IsConfigured);
    }

    [Theory]
    [InlineData("dpapi:v1:not-a-transport-key")]
    [InlineData("  dpapi:v1:not-a-transport-key  ")]
    public void RemoteResponseCannotInjectALocalProtectedCredentialEnvelope(string apiKey)
    {
        var profiles = new ObservableCollection<CopilotProfileConfig>();
        var remoteProfile = CreateRemoteProfile(
            "managed-1",
            "https://models.example.com/v1",
            allowInsecureHttp: false);
        remoteProfile.ApiKey = apiKey;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotBackendSyncClient.MergeProfiles(
                profiles,
                CreateResponse(remoteProfile),
                "https://sync.example.com"));

        Assert.Contains("protected-credential envelope", exception.Message, StringComparison.Ordinal);
        Assert.Empty(profiles);
    }

    [Fact]
    public void InvalidResponseLeavesExistingProfilesUnchanged()
    {
        var existing = new CopilotProfileConfig
        {
            Id = "local-id",
            Name = "Existing",
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "old-key",
            BaseUrl = "https://old.example.com/v1",
            Model = "old-model",
            SyncSource = "https://sync.example.com",
            SyncProfileId = "managed-1",
        };
        var stale = CreateLocalProfile(
            "stale-local-id",
            "https://sync.example.com",
            "stale-managed-id");
        var profiles = new ObservableCollection<CopilotProfileConfig> { existing, stale };
        var response = CreateResponse(
            CreateRemoteProfile("managed-1", "https://new.example.com/v1", false),
            CreateRemoteProfile("managed-2", "http://unsafe.example.com/v1", true));

        Assert.Throws<InvalidOperationException>(() =>
            CopilotBackendSyncClient.MergeProfiles(
                profiles,
                response,
                "https://sync.example.com"));

        Assert.Collection(
            profiles,
            item => Assert.Same(existing, item),
            item => Assert.Same(stale, item));
        Assert.Equal("Existing", existing.Name);
        Assert.Equal("old-key", existing.ApiKey);
        Assert.Equal("https://old.example.com/v1", existing.BaseUrl);
        Assert.Equal("old-model", existing.Model);
        Assert.Equal("managed-key", stale.ApiKey);
        Assert.Equal("https://models.example.com/v1", stale.BaseUrl);
    }

    private static CopilotBackendConfigResponse CreateResponse(
        params CopilotBackendProfile[] profiles) =>
        new()
        {
            SchemaVersion = 1,
            DefaultProfileId = profiles.FirstOrDefault()?.Id,
            Profiles = profiles.ToList(),
        };

    private static CopilotBackendProfile CreateRemoteProfile(
        string id,
        string baseUrl,
        bool allowInsecureHttp) =>
        new()
        {
            Id = id,
            Name = id,
            VendorType = nameof(CopilotVendorType.OpenAI),
            ProviderType = nameof(CopilotProviderType.OpenAICompatible),
            BaseUrl = baseUrl,
            Model = "gpt-test",
            ApiKey = "managed-key",
            AllowInsecureHttp = allowInsecureHttp,
            ReasoningMode = nameof(CopilotReasoningMode.Default),
        };

    private static CopilotProfileConfig CreateLocalProfile(
        string id,
        string syncSource,
        string syncProfileId) =>
        new()
        {
            Id = id,
            Name = id,
            VendorType = CopilotVendorType.OpenAI,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "managed-key",
            BaseUrl = "https://models.example.com/v1",
            Model = "gpt-test",
            SyncSource = syncSource,
            SyncProfileId = syncProfileId,
        };

    private static CopilotBackendDeviceIdentity CreateIdentity() =>
        new(
            "ColorVision",
            "1.0",
            "device",
            "Windows",
            "X64",
            "version-key");

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"schemaVersion\":1,\"profiles\":[]}"),
            });
        }
    }
}
