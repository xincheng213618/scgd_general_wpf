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
    [Fact]
    public async Task ToolExceptionIsRedactedBeforeTerminalResultIsPublished()
    {
        const string credential = "raw-mcp-secret-1234567890";
        var events = new List<CopilotAgentEvent>();
        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "secret-bearing-tool-failure",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = new SecretBearingFailureTool(credential),
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "exercise exception normalization",
                },
            },
            events.Add,
            CancellationToken.None);

        Assert.False(outcome.Result.Success);
        Assert.Contains("token=<redacted>", outcome.Result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, outcome.Result.ErrorMessage, StringComparison.Ordinal);
        Assert.True(outcome.Result.ErrorMessage.Length <= CopilotUserFacingErrorFormatter.MaximumMessageLength);

        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(outcome.Result.ErrorMessage, terminal.ToolResult?.ErrorMessage);
        Assert.DoesNotContain(credential, terminal.ToolResult?.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnedToolFailureDiagnosticsAreRedactedBeforePublication()
    {
        const string credential = "raw-returned-secret-1234567890";
        var events = new List<CopilotAgentEvent>();
        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "secret-bearing-tool-result",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = new SecretBearingResultTool(credential),
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "exercise result normalization",
                },
            },
            events.Add,
            CancellationToken.None);

        Assert.False(outcome.Result.Success);
        Assert.Equal("Remote token=<redacted>, request rejected.", outcome.Result.Summary);
        Assert.Equal("Authorization token=<redacted>; access denied.", outcome.Result.ErrorMessage);
        Assert.Equal("Diagnostic content remains available.", outcome.Result.Content);

        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(outcome.Result.Summary, terminal.Text);
        Assert.Equal(outcome.Result.ErrorMessage, terminal.ToolResult?.ErrorMessage);
        Assert.DoesNotContain(credential, terminal.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, terminal.ToolResult?.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

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

    private sealed class SecretBearingFailureTool(string credential) : ICopilotTool
    {
        public string Name => "SecretBearingFailureTool";

        public string Description => "Throws a test exception containing a credential.";

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromException<CopilotToolResult>(new InvalidOperationException(
                $"Remote tool failed with token={credential}. {new string('x', 1_000)}"));
        }
    }

    private sealed class SecretBearingResultTool(string credential) : ICopilotTool
    {
        public string Name => "SecretBearingResultTool";

        public string Description => "Returns a test failure containing a credential.";

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = $"Remote token={credential}, request rejected.",
                Content = "Diagnostic content remains available.",
                ErrorMessage = $"Authorization token={credential}; access denied.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }
    }
}
