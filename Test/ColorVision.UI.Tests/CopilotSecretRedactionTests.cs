using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotSecretRedactionTests
{
    [Theory]
    [InlineData(
        "rg sk-abcdefghijklmnopqrstuvwxyz123456",
        "rg <redacted>")]
    [InlineData(
        "echo AKIAABCDEFGHIJKLMNOP",
        "echo <redacted>")]
    public void RecognizableStandaloneCredentialsAreRedacted(
        string source,
        string expected)
    {
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
    }

    [Fact]
    public async Task BackgroundCommandPreviewRedactsCredentialButPreservesRawDigest()
    {
        const string credential = "sk-abcdefghijklmnopqrstuvwxyz123456";
        var command = $"echo {credential}";
        var request = new CopilotAgentRequest
        {
            ConversationId = "secret-redaction-test",
            TaskId = "task",
            Profile = CopilotProfileConfig.CreateDefault(),
            PreferredShell = CopilotShellKind.CommandPrompt,
        };
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["command"] = command,
                ["shell"] = "cmd",
                ["lifetimeSeconds"] =
                    CopilotBackgroundShellCommandRegistry.MinimumLifetimeSeconds,
            },
        };

        var registry = new CopilotBackgroundShellCommandRegistry();
        try
        {
            var started = await registry.StartAsync(
                request,
                input,
                CancellationToken.None);

            Assert.True(started.Success, started.ErrorMessage);
            var snapshot = Assert.IsType<CopilotBackgroundShellCommandSnapshot>(
                started.Snapshot);
            Assert.Equal("echo <redacted>", snapshot.CommandPreview);
            Assert.DoesNotContain(
                credential,
                snapshot.CommandPreview,
                StringComparison.Ordinal);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command)))
                    .ToLowerInvariant(),
                snapshot.CommandSha256);
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }
}
