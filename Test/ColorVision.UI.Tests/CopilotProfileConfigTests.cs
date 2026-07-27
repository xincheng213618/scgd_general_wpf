using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotProfileConfigTests
{
    [Fact]
    public void ProviderInactivityTimeouts_DefaultAndNormalize()
    {
        var profile = new CopilotProfileConfig();

        Assert.Equal(
            CopilotProfileConfig.DefaultFirstContentTimeoutSeconds,
            profile.FirstContentTimeoutSeconds);
        Assert.Equal(
            CopilotProfileConfig.DefaultStreamingInactivityTimeoutSeconds,
            profile.StreamingInactivityTimeoutSeconds);

        profile.FirstContentTimeoutSeconds = 0;
        profile.StreamingInactivityTimeoutSeconds = 1;

        Assert.Equal(
            CopilotProfileConfig.DefaultFirstContentTimeoutSeconds,
            profile.FirstContentTimeoutSeconds);
        Assert.Equal(
            CopilotProfileConfig.MinimumProviderInactivityTimeoutSeconds,
            profile.StreamingInactivityTimeoutSeconds);

        profile.FirstContentTimeoutSeconds = int.MaxValue;
        profile.StreamingInactivityTimeoutSeconds = int.MaxValue;

        Assert.Equal(
            CopilotProfileConfig.MaximumProviderInactivityTimeoutSeconds,
            profile.FirstContentTimeoutSeconds);
        Assert.Equal(
            CopilotProfileConfig.MaximumProviderInactivityTimeoutSeconds,
            profile.StreamingInactivityTimeoutSeconds);
    }

    [Fact]
    public void ProviderInactivityTimeouts_CloneAndRoundTrip()
    {
        var profile = new CopilotProfileConfig
        {
            FirstContentTimeoutSeconds = 840,
            StreamingInactivityTimeoutSeconds = 420,
        };

        var clone = profile.Clone();
        var serialized = JsonConvert.SerializeObject(profile);
        var restored = JsonConvert.DeserializeObject<CopilotProfileConfig>(serialized);

        Assert.Equal(840, clone.FirstContentTimeoutSeconds);
        Assert.Equal(420, clone.StreamingInactivityTimeoutSeconds);
        Assert.NotNull(restored);
        Assert.Equal(840, restored.FirstContentTimeoutSeconds);
        Assert.Equal(420, restored.StreamingInactivityTimeoutSeconds);
    }

    [Fact]
    public void ProviderInactivityTimeouts_LegacyProfileUsesDefaults()
    {
        var restored = JsonConvert.DeserializeObject<CopilotProfileConfig>(
            """{"Name":"Legacy","Model":"legacy-model"}""");

        Assert.NotNull(restored);
        Assert.Equal(
            CopilotProfileConfig.DefaultFirstContentTimeoutSeconds,
            restored.FirstContentTimeoutSeconds);
        Assert.Equal(
            CopilotProfileConfig.DefaultStreamingInactivityTimeoutSeconds,
            restored.StreamingInactivityTimeoutSeconds);
    }

    [Fact]
    public void ProviderInactivityPolicy_ResolvesProfileValues()
    {
        var profile = new CopilotProfileConfig
        {
            FirstContentTimeoutSeconds = 720,
            StreamingInactivityTimeoutSeconds = 360,
        };

        var timeouts = CopilotProviderInactivityPolicy.Resolve(profile);

        Assert.Equal(TimeSpan.FromSeconds(720), timeouts.FirstResponseTimeout);
        Assert.Equal(TimeSpan.FromSeconds(360), timeouts.StreamingUpdateTimeout);
    }
}
