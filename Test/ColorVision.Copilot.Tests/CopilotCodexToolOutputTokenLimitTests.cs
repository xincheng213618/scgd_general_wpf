using ColorVision.Copilot;
using System;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexToolOutputTokenLimitTests
{
    private const int SmallTokenLimit = 256;

    [Fact]
    public void UsesClosestTrustedLayerAndKeepsTheSubmittedRequestSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                tool_output_token_limit = 12_000

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string sourceDirectory = Path.Combine(projectRoot, "src");
            string configDirectory = Path.Combine(sourceDirectory, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "tool_output_token_limit = 2_048");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "tool_output_token_limit = 4_096");
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the current workspace.",
                CopilotAgentMode.Auto,
                submittedContext);
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var updatedSubmittedContext = submittedContext.WithConversationHistory(
                new CopilotConversationHistorySnapshot(
                    [new CopilotRequestMessage("user", "Continue")],
                    [new CopilotRequestMessage("user", "Continue")]));
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                sourceDirectory,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            var options = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(options.HasToolOutputTokenLimitOverride);
            Assert.Equal(2_048, options.ConfiguredToolOutputTokenLimit);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.ToolOutputTokenLimitSource);
            Assert.Equal(2_048, plan.ToolOutputTokenLimitOverride);
            Assert.Equal(2_048, request.ToolOutputTokenLimitOverride);
            Assert.Equal(2_048, updatedSubmittedContext.ProjectInstructionDiscoveryOptions.ConfiguredToolOutputTokenLimit);
            Assert.Equal(4_096, refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredToolOutputTokenLimit);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedOrInvalidProjectValuesCannotReplaceTheEffectiveLimit()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                tool_output_token_limit = 12_000

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(Path.Combine(configDirectory, "config.toml"), "tool_output_token_limit = 2_048");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.Equal(12_000, untrusted.ConfiguredToolOutputTokenLimit);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ToolOutputTokenLimitSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "tool_output_token_limit = -1");
            Assert.False(CopilotProjectInstructionDiscoveryConfig.Load(globalRoot).HasToolOutputTokenLimitOverride);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "tool_output_token_limit = 2_147_483_648");
            Assert.False(CopilotProjectInstructionDiscoveryConfig.Load(globalRoot).HasToolOutputTokenLimitOverride);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void MixedTextOutputFitsTheConfiguredTokenBudgetAndKeepsApprovalMetadata()
    {
        string originalContent = new string('界', 4_000) + new string('x', 12_000);
        var outcome = CreateOutcome(
            content: originalContent,
            approval: new CopilotToolApprovalInfo
            {
                ActionId = "approval-123",
                Title = "Apply the protected workspace change",
                RiskLevel = "confirmation-required",
                ExpiresAtUtc = DateTimeOffset.Parse("2026-08-08T12:00:00Z"),
            });

        string formatted = CopilotFrameworkToolResultFormatter.Format(
            outcome,
            SmallTokenLimit);
        using var document = JsonDocument.Parse(formatted);
        var root = document.RootElement;

        Assert.True(CopilotTokenEstimator.EstimateTextWeight(formatted)
            <= SmallTokenLimit
                * CopilotTokenEstimator.AsciiCharactersPerToken);
        Assert.Equal("awaiting_approval", root.GetProperty("status").GetString());
        Assert.Equal("approval-123", root.GetProperty("approval").GetProperty("action_id").GetString());
        Assert.True(root.GetProperty("content_truncated").GetBoolean());
        Assert.Equal(originalContent.Length, root.GetProperty("content_original_characters").GetInt32());
        Assert.True(root.GetProperty("content_returned_characters").GetInt32() < originalContent.Length);
        Assert.Equal(originalContent, outcome.Result.Content);
        Assert.Equal(@"C:\ColorVision\Source.cs", Assert.Single(outcome.Result.SuccessfullyReadLocalFilePaths));
    }

    [Fact]
    public void ConfiguredBudgetCanRetainMoreContentThanTheLegacyCharacterCap()
    {
        var outcome = CreateOutcome(new string('a', 30_000));

        string defaultFormatted = CopilotFrameworkToolResultFormatter.Format(outcome);
        string configuredFormatted = CopilotFrameworkToolResultFormatter.Format(outcome, 12_000);
        using var defaultDocument = JsonDocument.Parse(defaultFormatted);
        using var configuredDocument = JsonDocument.Parse(configuredFormatted);
        int defaultCharacters = defaultDocument.RootElement.GetProperty("content_returned_characters").GetInt32();
        string configuredContent = configuredDocument.RootElement.GetProperty("content").GetString() ?? string.Empty;

        Assert.True(defaultCharacters <= CopilotFrameworkToolResultFormatter.MaxContentCharacters);
        Assert.True(configuredContent.Length > CopilotFrameworkToolResultFormatter.MaxContentCharacters);
        Assert.True(CopilotTokenEstimator.EstimateTextWeight(configuredFormatted)
            <= 12_000 * CopilotTokenEstimator.AsciiCharactersPerToken);
    }

    [Fact]
    public void HeadTailCompactionDoesNotSplitUnicodeSurrogatePairs()
    {
        string originalContent = string.Concat(Enumerable.Repeat("😀a", 7_000))
            + new string('b', 20_000);

        string formatted = CopilotFrameworkToolResultFormatter.Format(CreateOutcome(originalContent));
        using var document = JsonDocument.Parse(formatted);
        string content = document.RootElement.GetProperty("content").GetString() ?? string.Empty;

        Assert.True(IsWellFormedUtf16(content));
        Assert.Contains("tool content compacted", content, StringComparison.Ordinal);
        Assert.True(document.RootElement.GetProperty("content_truncated").GetBoolean());
    }

    [Fact]
    public void RejectedOutputStaysValidAndPreservesFailureIdentityWithinTheBudget()
    {
        string formatted = CopilotFrameworkToolResultFormatter.FormatRejected(
            "RunShellCommand",
            new string('错', 5_000),
            "duplicate_call_id_conflict",
            CopilotToolFailureKind.Conflict,
            SmallTokenLimit);
        using var document = JsonDocument.Parse(formatted);

        Assert.True(CopilotTokenEstimator.EstimateTextWeight(formatted)
            <= SmallTokenLimit
                * CopilotTokenEstimator.AsciiCharactersPerToken);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("conflict", document.RootElement.GetProperty("failure_kind").GetString());
        Assert.Equal("duplicate_call_id_conflict", document.RootElement.GetProperty("failure_code").GetString());
    }

    [Fact]
    public void ZeroBudgetIsAValidCodexSettingAndStoresNoProviderVisiblePayload()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), "tool_output_token_limit = 0");
            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(options.HasToolOutputTokenLimitOverride);
            Assert.Equal(0, options.ConfiguredToolOutputTokenLimit);
            Assert.Equal(string.Empty, CopilotFrameworkToolResultFormatter.Format(CreateOutcome("content"), 0));
            Assert.Equal(
                string.Empty,
                CopilotFrameworkToolResultFormatter.FormatRejected(
                    "ReadLocalFile",
                    "rejected",
                    string.Empty,
                    CopilotToolFailureKind.Validation,
                    0));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void DiagnosticsExplainTheRequestSnapshotAndLocalEvidenceBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredToolOutputTokenLimit = 12_000,
            HasToolOutputTokenLimitOverride = true,
            ToolOutputTokenLimitSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ToolOutputTokenLimit = options.ConfiguredToolOutputTokenLimit,
            HasToolOutputTokenLimitOverride = true,
            ToolOutputTokenLimitSourceLabel = options.ToolOutputTokenLimitSourceLabel,
        });

        Assert.Contains("Codex tool_output_token_limit：12,000 Token", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ToolOutputTokenLimitSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("工具结果历史预算：单次最多 12,000 Token", contextReport, StringComparison.Ordinal);
        Assert.Contains("完整工具结果、审批记录、证据路径与审计日志保持原样", contextReport, StringComparison.Ordinal);
    }

    private static CopilotToolExecutionOutcome CreateOutcome(
        string content,
        CopilotToolApprovalInfo? approval = null)
    {
        return new CopilotToolExecutionOutcome
        {
            Result = new CopilotToolResult
            {
                ToolName = "ReadLocalFile",
                Success = true,
                Summary = "Read source evidence.",
                Content = content,
                Approval = approval,
                SuccessfullyReadLocalFilePaths = [@"C:\ColorVision\Source.cs"],
            },
            Execution = new CopilotToolExecutionInfo
            {
                ToolName = "ReadLocalFile",
                Attempt = 1,
                MaxAttempts = 2,
                RetryEligible = false,
            },
        };
    }

    private static bool IsWellFormedUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    return false;
                index++;
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-tool-output-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
