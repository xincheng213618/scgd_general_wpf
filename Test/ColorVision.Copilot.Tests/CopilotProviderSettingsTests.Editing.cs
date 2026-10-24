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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReassigningCurrentModelAndSmartModePreservesUncommittedFields(bool smart)
    {
        var profile = new CopilotProfileConfig { VendorType = CopilotVendorType.OpenAI, Model = "gpt-6-astra", UseModelDefaults = smart };
        var editor = new CopilotModelEditor(profile, [])
        {
            ContextWindowText = "256000", MaxOutputText = "8192", SupportsImages = true,
            FirstContentTimeoutText = "invalid draft", StreamingInactivityTimeoutText = "300"
        };

        editor.ModelId = " gpt-6-astra ";
        editor.SmartConfiguration = smart;

        Assert.Equal("256000", editor.ContextWindowText);
        Assert.Equal("8192", editor.MaxOutputText);
        Assert.True(editor.SupportsImages);
        Assert.Equal("invalid draft", editor.FirstContentTimeoutText);
        Assert.Equal("300", editor.StreamingInactivityTimeoutText);
    }

    [Theory]
    [InlineData("invalid", "120", "首段内容超时")]
    [InlineData("", "120", "首段内容超时")]
    [InlineData("14", "120", "首段内容超时")]
    [InlineData("180", "3601", "流式响应间隔")]
    [InlineData("180", "invalid", "流式响应间隔")]
    public void ModelEditorRejectsInvalidTimeoutWithoutCommittingOtherDraftFields(string firstContent, string inactivity, string errorField)
    {
        var profile = CopilotProfileConfig.CreateDefault();
        var initial = profile.Clone();
        var editor = new CopilotModelEditor(profile, [])
        {
            ContextWindowText = "256000", MaxOutputText = "8192",
            FirstContentTimeoutText = firstContent, StreamingInactivityTimeoutText = inactivity
        };

        Assert.False(editor.Validate());
        Assert.Contains(errorField, editor.Error);
        Assert.Equal(initial.ContextWindowTokens, profile.ContextWindowTokens);
        Assert.Equal(initial.MaxTokens, profile.MaxTokens);
        Assert.Equal(initial.FirstContentTimeoutSeconds, profile.FirstContentTimeoutSeconds);
        Assert.Equal(initial.StreamingInactivityTimeoutSeconds, profile.StreamingInactivityTimeoutSeconds);
        Assert.Equal(firstContent, editor.FirstContentTimeoutText);
        Assert.Equal(inactivity, editor.StreamingInactivityTimeoutText);
    }

    [Fact]
    public void ModelEditorCommitsTimeoutBoundsAndResetRestoresOriginalValues()
    {
        var profile = new CopilotProfileConfig { VendorType = CopilotVendorType.OpenAI, Model = "custom-model", UseModelDefaults = false };
        var editor = new CopilotModelEditor(profile, [])
        {
            FirstContentTimeoutText = "15", StreamingInactivityTimeoutText = "3600"
        };
        editor.ModelId = "gpt-6-astra";
        editor.SmartConfiguration = true;
        Assert.Equal("15", editor.FirstContentTimeoutText);
        Assert.Equal("3600", editor.StreamingInactivityTimeoutText);
        Assert.Equal("1050000", editor.ContextWindowText);
        Assert.True(editor.SupportsImages);
        Assert.True(editor.Validate(), editor.Error);
        Assert.Equal(15, profile.FirstContentTimeoutSeconds);
        Assert.Equal(3600, profile.StreamingInactivityTimeoutSeconds);

        editor.FirstContentTimeoutText = "invalid";
        Assert.False(editor.Validate());
        editor.Reset();
        Assert.Empty(editor.Error);
        Assert.Equal("custom-model", editor.ModelId);
        Assert.False(editor.SmartConfiguration);
        Assert.Equal(CopilotProfileConfig.DefaultFirstContentTimeoutSeconds.ToString(), editor.FirstContentTimeoutText);
        Assert.Equal(CopilotProfileConfig.DefaultStreamingInactivityTimeoutSeconds.ToString(), editor.StreamingInactivityTimeoutText);
        Assert.True(editor.Validate(), editor.Error);
    }
}
