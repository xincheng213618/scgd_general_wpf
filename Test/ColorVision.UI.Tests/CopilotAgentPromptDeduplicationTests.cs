using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentPromptDeduplicationTests
{
    [Fact]
    public void StandardHarnessKeepsAnswerRequirementsInOneInstructionSurface()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Code,
            UserText = "Explain the implementation from the supplied ColorVision context.",
            CodexSandboxMode = CopilotCodexSandboxMode.ReadOnly,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(
                CopilotCodexApprovalPolicyMode.Untrusted),
            CodexApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
            CodexAutoReviewPolicy = "allow read-only inspection",
        };
        var contextBuilder = new CopilotAgentContextBuilder();

        var fullPrompt = contextBuilder.BuildAnswerMessages(
            request,
            Array.Empty<CopilotAgentStepRecord>());
        var harnessPrompt = contextBuilder.BuildHarnessMessages(
            request,
            Array.Empty<CopilotAgentStepRecord>(),
            minimalDelegatedFinalization: false);
        var harnessInstructions = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            [new CopilotTemplatePatchTool()],
            CopilotAgentEnvironmentContext.Capture(request),
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        var submittedInstructions = harnessInstructions + "\n" + harnessPrompt.PreparedUserMessageContent;

        Assert.Contains("# Answer requirements", fullPrompt.PreparedUserMessageContent, StringComparison.Ordinal);
        Assert.DoesNotContain("# Answer requirements", harnessPrompt.PreparedUserMessageContent, StringComparison.Ordinal);
        Assert.True(
            fullPrompt.PreparedUserMessageContent.Length - harnessPrompt.PreparedUserMessageContent.Length > 1_000,
            "The standard Harness prompt should materially remove repeated per-turn instructions.");
        Assert.Equal(1, CountOccurrences(
            submittedInstructions,
            "Do not end with a request for more context."));
        Assert.Equal(1, CountOccurrences(
            submittedInstructions,
            CopilotAgentContextBuilder.BuildModeInstruction(CopilotAgentMode.Code)));
        Assert.Equal(1, CountOccurrences(
            submittedInstructions,
            CopilotCodexApprovalPolicySelection.GetModelInstruction(request.CodexApprovalPolicy)));
        Assert.Equal(1, CountOccurrences(
            submittedInstructions,
            CopilotCodexApprovalsReviewerSelection.GetModelInstruction(request.CodexApprovalsReviewer)));
        Assert.Equal(1, CountOccurrences(submittedInstructions, "Codex auto_review.policy is frozen"));
        Assert.Contains("omit that fact instead of guessing", submittedInstructions, StringComparison.Ordinal);
        Assert.Contains("sandbox_mode=read-only is frozen", submittedInstructions, StringComparison.Ordinal);
        Assert.DoesNotContain("sandbox_mode=read-only applies", submittedInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void NoToolsRecoveryAndMinimalDelegatedFinalizationKeepSelfContainedRequirements()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = "Return the supported final answer.",
        };
        var contextBuilder = new CopilotAgentContextBuilder();

        var recoveryPrompt = contextBuilder.BuildAnswerMessages(
            request,
            Array.Empty<CopilotAgentStepRecord>());
        var delegatedFinalizationPrompt = contextBuilder.BuildHarnessMessages(
            request,
            Array.Empty<CopilotAgentStepRecord>(),
            minimalDelegatedFinalization: true);

        Assert.Contains("# Answer requirements", recoveryPrompt.PreparedUserMessageContent, StringComparison.Ordinal);
        Assert.Contains("# Answer requirements", delegatedFinalizationPrompt.PreparedUserMessageContent, StringComparison.Ordinal);
        Assert.Equal(
            recoveryPrompt.PreparedUserMessageContent,
            delegatedFinalizationPrompt.PreparedUserMessageContent);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
