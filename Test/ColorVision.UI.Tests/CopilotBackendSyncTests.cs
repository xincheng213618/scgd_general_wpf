using ColorVision.Copilot;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotBackendSyncTests
{
    [Fact]
    public void BuildEndpoint_BlocksRemoteHttpUnlessExplicitlyAllowed()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            CopilotBackendSyncClient.BuildEndpoint("http://xc213618.ddns.me:9998/", allowInsecureHttp: false));

        Assert.Contains("without transport encryption", error.Message, StringComparison.Ordinal);
        Assert.Equal(
            "http://xc213618.ddns.me:9998/api/copilot/config",
            CopilotBackendSyncClient.BuildEndpoint(
                "http://xc213618.ddns.me:9998/",
                allowInsecureHttp: true).AbsoluteUri);
        Assert.Equal(
            "http://127.0.0.1:9998/api/copilot/config",
            CopilotBackendSyncClient.BuildEndpoint(
                "http://127.0.0.1:9998",
                allowInsecureHttp: false).AbsoluteUri);
    }

    [Fact]
    public void MergeProfiles_ReconcilesOnlyProfilesFromSameBackend()
    {
        var local = new CopilotProfileConfig
        {
            Id = "local",
            Name = "Local profile",
            ApiKey = "local-key",
            BaseUrl = "https://local.example/v1",
            Model = "local-model",
        };
        var existing = new CopilotProfileConfig
        {
            Id = "existing-local-id",
            Name = "Old remote name",
            ApiKey = "old-key",
            BaseUrl = "https://old.example/v1",
            Model = "old-model",
            SyncSource = "http://xc213618.ddns.me:9998",
            SyncProfileId = "remote-one",
        };
        var stale = new CopilotProfileConfig
        {
            Id = "stale-local-id",
            Name = "Stale",
            ApiKey = "stale-key",
            BaseUrl = "https://stale.example/v1",
            Model = "stale-model",
            SyncSource = "http://xc213618.ddns.me:9998",
            SyncProfileId = "removed",
        };
        var profiles = new ObservableCollection<CopilotProfileConfig> { local, existing, stale };
        var response = new CopilotBackendConfigResponse
        {
            SchemaVersion = 1,
            DefaultProfileId = "remote-two",
            Profiles =
            [
                BackendProfile("remote-one", "Updated remote", "model-one"),
                BackendProfile("remote-two", "New remote", "model-two"),
            ],
        };

        var result = CopilotBackendSyncClient.MergeProfiles(
            profiles,
            response,
            "http://xc213618.ddns.me:9998/");

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Removed);
        Assert.Contains(profiles, profile => ReferenceEquals(profile, local));
        Assert.DoesNotContain(profiles, profile => profile.Id == "stale-local-id");
        var updated = Assert.Single(profiles, profile => profile.SyncProfileId == "remote-one");
        Assert.Equal("existing-local-id", updated.Id);
        Assert.Equal("Updated remote", updated.Name);
        Assert.Equal("new-provider-key", updated.ApiKey);
        var added = Assert.Single(profiles, profile => profile.SyncProfileId == "remote-two");
        Assert.Equal(added.Id, result.DefaultLocalProfileId);
    }

    [Fact]
    public async Task FetchAsync_SendsScopedBearerTokenAndParsesResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "schemaVersion": 1,
                      "revision": "rev-1",
                      "generatedAt": "2026-07-25T12:00:00Z",
                      "defaultProfileId": "remote-one",
                      "profiles": [{
                        "id": "remote-one",
                        "name": "Shared",
                        "vendorType": "DeepSeek",
                        "providerType": "AnthropicCompatible",
                        "baseUrl": "https://api.deepseek.com/anthropic",
                        "model": "deepseek-v4-pro",
                        "apiKey": "provider-key",
                        "allowInsecureHttp": false,
                        "reasoningMode": "High",
                        "isDefault": true
                      }]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        using var httpClient = new HttpClient(handler);
        var client = new CopilotBackendSyncClient(httpClient);

        var result = await client.FetchAsync(
            "https://config.example/",
            "cvmp_sync_token",
            allowInsecureHttp: false,
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("cvmp_sync_token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Equal("https://config.example/api/copilot/config", capturedRequest.RequestUri?.AbsoluteUri);
        Assert.Equal("rev-1", result.Revision);
        Assert.Equal("provider-key", Assert.Single(result.Profiles).ApiKey);
    }

    private static CopilotBackendProfile BackendProfile(string id, string name, string model)
    {
        return new CopilotBackendProfile
        {
            Id = id,
            Name = name,
            VendorType = "DeepSeek",
            ProviderType = "AnthropicCompatible",
            BaseUrl = "https://api.deepseek.com/anthropic",
            Model = model,
            ApiKey = "new-provider-key",
            ReasoningMode = "High",
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
