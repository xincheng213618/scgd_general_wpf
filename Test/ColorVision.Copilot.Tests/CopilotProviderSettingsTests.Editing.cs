namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotProviderSettingsTests
{
    [Fact]
    public void SharedConnectionEditPublishesOneConsistentSnapshotAndIgnoresIdenticalValues()
    {
        var models = Enumerable.Range(0, 4).Select(index => new CopilotProfileConfig
        {
            ConnectionId = "shared-connection", ConnectionName = "Shared", Model = "custom-" + index,
            BaseUrl = "https://example.test/v1", ApiKey = "old-test-key", UseModelDefaults = false, SupportsImageInput = true
        }).ToArray();
        var snapshots = new List<string[]>();
        using var provider = new CopilotProviderSettings(models, () => snapshots.Add(models.Select(model => model.ApiKey).ToArray()));

        provider.ApiKey = "new-test-key";

        Assert.All(Assert.Single(snapshots), key => Assert.Equal("new-test-key", key));
        snapshots.Clear();
        provider.ApiKey = " new-test-key ";
        provider.BaseUrl = " https://example.test/v1 ";
        provider.ProviderType = provider.ProviderType;
        provider.Name = provider.Name;
        provider.IsEnabled = provider.IsEnabled;
        provider.AllowInsecureHttp = provider.AllowInsecureHttp;
        Assert.Empty(snapshots);
        Assert.All(models, model => Assert.True(model.SupportsImageInput));

        provider.Name = "Renamed";
        Assert.Single(snapshots);
        Assert.All(models, model => Assert.Equal("Renamed", model.ConnectionName));
    }

}
