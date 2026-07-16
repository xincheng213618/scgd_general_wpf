using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotUserFacingErrorFormatterTests
{
    [Fact]
    public void SanitizeUsesFallbackForEmptyError()
    {
        Assert.Equal("Unknown error.", CopilotUserFacingErrorFormatter.Sanitize(" \r\n\t"));
    }

    [Fact]
    public void SanitizeRedactsExplicitAndLabelledSecretsAndBoundsOutput()
    {
        const string apiKey = "unlabelled-provider-secret";
        var raw = $"Bearer bearer-secret\r\npassword=labelled-secret\0 Echoed credential: {apiKey} " + new string('x', 1_000);

        var sanitized = CopilotUserFacingErrorFormatter.Sanitize(raw, apiKey);

        Assert.Contains("Bearer <redacted>", sanitized, StringComparison.Ordinal);
        Assert.Contains("password=<redacted>", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("labelled-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain('\0', sanitized);
        Assert.Equal(CopilotUserFacingErrorFormatter.MaximumMessageLength, sanitized.Length);
        Assert.EndsWith("...", sanitized, StringComparison.Ordinal);
    }
}
