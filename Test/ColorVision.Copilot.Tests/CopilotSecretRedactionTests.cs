using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

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

    [Theory]
    [InlineData(
        "Bearer abcde+fghijklmnopqrstuvwxyz012345",
        "Bearer <redacted>")]
    [InlineData(
        "bEaReR\tabcdefghijklmnop",
        "Bearer <redacted>")]
    [InlineData(
        "Bearer   AbcdefghijklMN09._~+/-==; echo done",
        "Bearer <redacted>; echo done")]
    [InlineData(
        "Bearer abcdefghijklmnop\")",
        "Bearer <redacted>\")")]
    public void SupportedBearerCredentialsAreFullyRedactedWithoutConsumingDelimiters(
        string source,
        string expected)
    {
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
    }

    [Theory]
    [InlineData("Bearer of good news")]
    [InlineData("Bearer abcdefghijklmno")]
    [InlineData("NotABearer abcdefghijklmnop")]
    [InlineData("Bearerabcdefghijklmnop")]
    [InlineData("Bearer\nabcdefghijklmnop")]
    [InlineData("Bearer\u00a0abcdefghijklmnop")]
    [InlineData("Bearer abcdefghijklmno\u212a")]
    public void BearerLikeTextOutsideCredentialBoundariesIsPreserved(string source)
    {
        Assert.Equal(source, CopilotMcpAuditLogger.RedactText(source));
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

    [Fact]
    public async Task McpToolAuditKeepsFieldNamesAndFailureCodeWithoutPayloadValuesOrWorkspacePath()
    {
        const string rawSessionToken = "raw-mcp-session-token-that-must-not-be-audited";
        const string rawWorkspacePath = @"C:\Customers\SensitiveWorkspace";
        const string argumentValue = "private-locale-value-that-must-not-be-audited";
        var executionScope = CopilotExecutionScope.ForExternalMcpSession(
            rawSessionToken,
            "test-caller",
            rawWorkspacePath);
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = rawWorkspacePath,
                SearchRootPaths = [rawWorkspacePath],
            },
        });
        var arguments = new Dictionary<string, JsonElement>
        {
            ["language"] = JsonSerializer.SerializeToElement(argumentValue),
        };

        CopilotMcpAuditLogger.ClearForTests();
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        try
        {
            var result = await dispatcher.CallExternalAsync(
                "set_language",
                arguments,
                executionScope,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("confirmation_required", result.ErrorCode);
            var entries = CopilotMcpAuditLogger.GetRecentEntries(200);
            var toolEntry = Assert.Single(entries.Where(entry => entry.ToolName == "set_language"));
            Assert.Equal("fields=language", toolEntry.ArgumentSummary);
            Assert.Equal("confirmation_required", toolEntry.ErrorMessage);
            Assert.Equal(executionScope.WorkspaceIdentity, toolEntry.WorkspaceIdentity);
            Assert.DoesNotContain(rawWorkspacePath, toolEntry.WorkspaceIdentity, StringComparison.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var retainedText = string.Join('|',
                    entry.ArgumentSummary,
                    entry.ErrorMessage,
                    entry.WorkspaceIdentity,
                    entry.ApprovalDecisionReason);
                Assert.DoesNotContain(argumentValue, retainedText, StringComparison.Ordinal);
                Assert.DoesNotContain(rawWorkspacePath, retainedText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(rawSessionToken, retainedText, StringComparison.Ordinal);
            }
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.ClearForTests();
            CopilotMcpAuditLogger.ClearForTests();
        }
    }

    [Fact]
    public void McpActionAuditProjectsArgumentsApprovalReasonAndWorkspace()
    {
        const string sensitiveDetail = "private-review-detail-that-must-not-be-audited";
        const string rawWorkspacePath = @"C:\Customers\SensitiveWorkspace";
        var executionScope = CopilotExecutionScope.ForExternalMcpSession(
            "raw-session-token",
            "test-caller",
            rawWorkspacePath);
        var action = new ConfirmableAction
        {
            ActionId = "action-1",
            ToolName = "set_language",
            ArgumentsSummary = "language=" + sensitiveDetail,
            ApprovalDecisionSource = "automatic-review",
            ApprovalDecisionReason = sensitiveDetail,
            RequestContext = new CopilotConfirmationRequestContext
            {
                Scope = executionScope,
                SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                RequestSource = "test-caller",
                WorkspacePath = rawWorkspacePath,
            },
        };

        CopilotMcpAuditLogger.ClearForTests();
        try
        {
            CopilotMcpAuditLogger.ActionApproved(action);

            var entry = Assert.Single(CopilotMcpAuditLogger.GetRecentEntries(1));
            Assert.Equal("details-withheld", entry.ArgumentSummary);
            Assert.Equal("details-withheld", entry.ApprovalDecisionReason);
            Assert.Equal(executionScope.WorkspaceIdentity, entry.WorkspaceIdentity);
            Assert.DoesNotContain(sensitiveDetail, entry.ArgumentSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitiveDetail, entry.ApprovalDecisionReason, StringComparison.Ordinal);
            Assert.DoesNotContain(rawWorkspacePath, entry.WorkspaceIdentity, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CopilotMcpAuditLogger.ClearForTests();
        }
    }
}
