using ColorVision.Copilot.Mcp;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSharedTextRedactionTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("{\"Authorization\":\"Digest name=\\\"alpha\\\", note=\\\"beta gamma\\\"\",\"Mode\":\"Run\"}", "{\"Authorization\":\"<redacted>\",\"Mode\":\"Run\"}")]
    [InlineData("Authorization: Digest user=\"alpha\", response=\"omega\"; realm=\"private\"\r\nMode: Run", "Authorization: <redacted>\r\nMode: Run")]
    [InlineData("prefix Authorization=Digest username=\"alpha\", realm=\"beta gamma\"; Mode=Run", "prefix Authorization=<redacted>; Mode=Run")]
    [InlineData("Bearer abcdefghijklmnop; sk-A0b1C2d3E4f5G6h7I8j9; AKIA0123456789ABCDEF; Mode=Run", "Bearer <redacted>; <redacted>; <redacted>; Mode=Run")]
    [InlineData("Bearer shortsecret; sk-0123456789abcdefghi; xAKIA0123456789ABCDEF; Mode=Run", "Bearer shortsecret; sk-0123456789abcdefghi; xAKIA0123456789ABCDEF; Mode=Run")]
    [InlineData("Mode=\"Normal, with; delimiters\"; next=visible", "Mode=\"Normal, with; delimiters\"; next=visible")]
    public void CommonAuditAndBusinessTextShareExistingWholeTextGoldens(string? source, string expected)
    {
        Assert.Equal(expected, CopilotSensitiveTextRedactor.Redact(source));
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactArgument("message", source));
        Assert.Equal(expected, CopilotBusinessContextBuilder.MaskSensitiveText(source));
    }

    [Theory]
    [InlineData("password=\"alpha, beta; escaped\\\"omega\"; Mode=Run; trace=visible", "password=<redacted>; Mode=Run; trace=visible")]
    [InlineData("password='alpha, beta; omega'; Mode=Run; trace=visible", "password=<redacted>; Mode=Run; trace=visible")]
    [InlineData("{\"api_key\":\"alpha, beta; escaped\\\"omega\",\"Mode\":\"Run\",\"trace\":\"visible\"}", "{\"api_key\":<redacted>,\"Mode\":\"Run\",\"trace\":\"visible\"}")]
    [InlineData("password=\"alpha, beta; omega", "password=<redacted>")]
    public void QuotedAndUnclosedAssignmentsNormalizeValuesAndPreserveFollowingFields(string source, string expected)
    {
        Assert.Equal(expected, CopilotSensitiveTextRedactor.Redact(source));
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactArgument("message", source));
        Assert.Equal(expected, CopilotBusinessContextBuilder.MaskSensitiveText(source));
        Assert.Contains("<redacted>", expected, StringComparison.Ordinal);
        foreach (var fragment in new[] { "alpha", "beta", "omega" })
            Assert.DoesNotContain(fragment, expected, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Authorization: Basic dXNlcjpwYXNz\r\nMode: Run", "Authorization: <redacted>\r\nMode: Run")]
    [InlineData("Bearer abcdefghijklmnop; sk-A0b1C2d3E4f5G6h7I8j9; AKIA0123456789ABCDEF", "Bearer <redacted>; <redacted>; <redacted>")]
    [InlineData("password=\"stream-owned, quoted; value\"; Mode=Run", "password=\"stream-owned, quoted; value\"; Mode=Run")]
    public void ArchiveSourceRulesKeepGenericQuoteOwnershipInTheStreamingArchive(string source, string expected)
    {
        Assert.Equal(expected, CopilotSensitiveTextRedactor.RedactArchiveSource(source));
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactArchiveSource(source));
    }
}
