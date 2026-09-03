using ColorVision.Copilot;
using System;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexToolOutputTokenLimitTests
{
    private const int SmallTokenLimit = 256;

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
    public void ZeroBudgetStoresNoProviderVisiblePayload()
    {
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
}
