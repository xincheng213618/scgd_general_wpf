using ColorVision.UI;
using System.Globalization;

namespace ColorVision.UI.Tests;

public sealed class CopilotContextTextPrivacyTests
{
    [Fact]
    public void FlowAndDeviceLogContextsHideCompleteCredentialFieldsAndPreserveFollowingEvidenceWithoutChangingSources()
    {
        string source = string.Join("\r\n", new[]
        {
            "Authorization: Digest username=\"fixture-user\", realm=\"fixture-realm\", nonce=\"fixture-nonce\"",
            "Proxy-Authorization: Basic fixture-proxy-value",
            "password=\"fixture first; fixture second, \\\"fixture escaped\\\" tail\"; mode=Production",
            "{\"密钥\":\"fixture localized, tail\",\"status\":\"ready\"}",
            "license='fixture license; tail'; count=17",
            "SN=\"fixture serial, tail\"; complete=true",
            "trace complete",
        });
        var flow = new CopilotFlowContextSnapshot { FlowName = "Offline flow", RecentRunMessage = source };
        var device = new CopilotDeviceContextSnapshot { ServiceName = "Offline service", RecentLogSummary = source, RecentLogContent = source };

        var flowContext = CopilotBusinessContextBuilder.BuildFlowContextItem(flow);
        var deviceContext = CopilotBusinessContextBuilder.BuildDeviceContextItem(device);

        foreach (string text in new[] { flowContext.Content, deviceContext.Content })
        {
            foreach (string secret in new[] { "fixture-user", "fixture-realm", "fixture-nonce", "fixture-proxy-value",
                "fixture first", "fixture second", "fixture escaped", "fixture localized", "fixture license", "fixture serial" })
                Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
            Assert.Contains("Authorization: <redacted>", text, StringComparison.Ordinal);
            Assert.Contains("Proxy-Authorization: <redacted>", text, StringComparison.Ordinal);
            Assert.Contains("mode=Production", text, StringComparison.Ordinal);
            Assert.Contains("\"status\":\"ready\"", text, StringComparison.Ordinal);
            Assert.Contains("count=17", text, StringComparison.Ordinal);
            Assert.Contains("complete=true", text, StringComparison.Ordinal);
            Assert.Contains("trace complete", text, StringComparison.Ordinal);
        }
        Assert.Equal(source, flow.RecentRunMessage);
        Assert.Equal(source, device.RecentLogSummary);
        Assert.Equal(source, device.RecentLogContent);
    }

    [Theory]
    [InlineData("password=\"alpha, beta; \\\"gamma\\\" tail\"; mode=ok", "password=<redacted>; mode=ok")]
    [InlineData("{\"密钥\":\"alpha, beta\",\"mode\":\"ok\"}", "{\"密钥\":<redacted>,\"mode\":\"ok\"}")]
    [InlineData("license='alpha; beta'; mode=ok", "license=<redacted>; mode=ok")]
    [InlineData("password=\"alpha, beta; tail", "password=<redacted>")]
    [InlineData("password=\"alpha\"omega; mode=ok", "password=<redacted>; mode=ok")]
    [InlineData("password=\"alpha\"omega mode=ok", "password=<redacted> mode=ok")]
    [InlineData("[password=\"alpha\"omega], mode=ok", "[password=<redacted>], mode=ok")]
    [InlineData("(password=\"alpha\"omega); mode=ok", "(password=<redacted>); mode=ok")]
    [InlineData("sk-123456789012password=alpha; mode=ok", "<redacted>=<redacted>; mode=ok")]
    [InlineData("{\"Authorization\":\"\",\"mode\":\"ok\"}", "{\"Authorization\":\"<redacted>\",\"mode\":\"ok\"}")]
    public void QuotedFieldsNormalizeValuesAndDoNotExposeEscapedOrUnclosedSecretTails(string source, string expected)
    {
        Assert.Equal(expected, CopilotSensitiveTextRedactor.Redact(source));
        Assert.Equal(expected, CopilotBusinessContextBuilder.MaskSensitiveText(source));
    }

    [Fact]
    public void HeaderAndAssignmentRedactionRemainStableInTurkishCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            const string source = "AUTHORIZATION: Basic fixture-header\r\nPRIVATE_KEY=\"fixture private, tail\"; API Key='fixture key'; mode=Production";
            const string expected = "AUTHORIZATION: <redacted>\r\nPRIVATE_KEY=<redacted>; API Key=<redacted>; mode=Production";

            Assert.Equal(expected, CopilotSensitiveTextRedactor.Redact(source));
            Assert.Equal(expected, CopilotBusinessContextBuilder.MaskSensitiveText(source));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
