using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAutomaticApprovalOverrideTests
{
    [Fact]
    public void ExactOverrideMatchesConversationWorkspaceToolAndDigestOnceAcrossRunIds()
    {
        var store = new CopilotAutomaticApprovalOverrideStore();
        var workspacePath = Path.GetFullPath(Path.GetTempPath());
        var tool = new CopilotShellCommandTool();
        var denial = CreateDeniedAction(
            "denial-exact",
            "conversation-exact",
            "run-original",
            workspacePath,
            tool.Name,
            new string('a', 64));
        store.RecordDenial(denial);

        var recent = Assert.Single(store.GetRecentDenials("conversation-exact", workspacePath));
        Assert.True(store.TryAuthorizeOneRetry(
            recent.DenialId,
            "conversation-exact",
            workspacePath,
            out var authorized));
        Assert.Equal("run-original", authorized.TaskId);
        Assert.False(store.TryAuthorizeOneRetry(
            recent.DenialId,
            "conversation-exact",
            workspacePath,
            out _));

        var retryRequest = CreateRequest("conversation-exact", "run-retry", workspacePath);
        Assert.False(store.TryConsume(
            retryRequest,
            tool,
            CreatePendingAction(retryRequest, tool.Name, new string('b', 64))));
        Assert.False(store.TryConsume(
            retryRequest,
            tool,
            CreatePendingAction(retryRequest, "DifferentProtectedTool", new string('a', 64))));
        Assert.False(store.TryConsume(
            CreateRequest("another-conversation", "run-retry", workspacePath),
            tool,
            CreatePendingAction(
                CreateRequest("another-conversation", "run-retry", workspacePath),
                tool.Name,
                new string('a', 64))));
        Assert.False(store.TryConsume(
            CreateRequest("conversation-exact", "run-retry", workspacePath + "-other"),
            tool,
            CreatePendingAction(
                CreateRequest("conversation-exact", "run-retry", workspacePath + "-other"),
                tool.Name,
                new string('a', 64))));

        var exactRetry = CreatePendingAction(retryRequest, tool.Name, new string('a', 64));
        Assert.True(store.TryConsume(retryRequest, tool, exactRetry));
        Assert.True(exactRetry.HasAutomaticReviewRetryOverride);

        var repeatedRetry = CreatePendingAction(retryRequest, tool.Name, new string('a', 64));
        Assert.False(store.TryConsume(retryRequest, tool, repeatedRetry));
        Assert.False(repeatedRetry.HasAutomaticReviewRetryOverride);
    }

    [Fact]
    public void RecentDenialsAreBoundedPerConversationAndPickerDoesNotExposeIdentifiers()
    {
        var store = new CopilotAutomaticApprovalOverrideStore();
        var workspacePath = Path.Combine(Path.GetTempPath(), "override-private-workspace");
        var tool = new CopilotShellCommandTool();
        for (var index = 0; index < 12; index++)
        {
            store.RecordDenial(CreateDeniedAction(
                $"private-action-{index}",
                "conversation-capacity",
                $"run-{index}",
                workspacePath,
                tool.Name,
                index.ToString("x64"),
                DateTimeOffset.UtcNow.AddMinutes(index)));
        }

        var recent = store.GetRecentDenials("conversation-capacity", workspacePath);
        Assert.Equal(CopilotAutomaticApprovalOverrideStore.MaximumRecentDenialsPerConversation, recent.Count);
        Assert.Equal("private-action-11", recent[0].DenialId);
        Assert.DoesNotContain(recent, item => item.DenialId == "private-action-0");
        Assert.DoesNotContain(recent, item => item.DenialId == "private-action-1");

        var picker = CopilotAutomaticApprovalDenialCommand.Evaluate(
            recent,
            string.Empty,
            DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.False(picker.AuthorizesRetry);
        Assert.Contains("仍会经过自动审查", picker.Report, StringComparison.Ordinal);
        Assert.Contains(tool.Name, picker.Report, StringComparison.Ordinal);
        Assert.DoesNotContain("private-action", picker.Report, StringComparison.Ordinal);
        Assert.DoesNotContain("override-private-workspace", picker.Report, StringComparison.Ordinal);
        Assert.DoesNotContain(recent[0].ArgumentsDigest, picker.Report, StringComparison.Ordinal);

        var selected = CopilotAutomaticApprovalDenialCommand.Evaluate(
            recent,
            "1",
            DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.True(selected.AuthorizesRetry);
        Assert.Same(recent[0], selected.Denial);
    }

    [Fact]
    public void UnavailableOrUserRejectedActionsDoNotCreateRetryOverrides()
    {
        var store = new CopilotAutomaticApprovalOverrideStore();
        var workspacePath = Path.GetFullPath(Path.GetTempPath());
        var tool = new CopilotShellCommandTool();
        store.RecordDenial(CreateDeniedAction(
            "unavailable-action",
            "conversation-filter",
            "run-unavailable",
            workspacePath,
            tool.Name,
            new string('d', 64),
            decisionSource: "automatic-review-unavailable"));
        store.RecordDenial(CreateDeniedAction(
            "user-rejected-action",
            "conversation-filter",
            "run-user",
            workspacePath,
            tool.Name,
            new string('e', 64),
            decisionSource: "user"));

        Assert.Empty(store.GetRecentDenials("conversation-filter", workspacePath));
    }

    [Fact]
    public void ReviewerOverrideMarkerIsDeveloperScopedAndDoesNotBecomeEvidenceOrPermission()
    {
        var workspacePath = Path.GetFullPath(Path.GetTempPath());
        var request = CreateRequest(
            "conversation-marker",
            "run-marker",
            workspacePath,
            CopilotCodexApprovalsReviewer.AutoReview);
        var tool = new CopilotShellCommandTool();
        var action = CreatePendingAction(request, tool.Name, new string('c', 64));

        var ordinaryInstructions = CopilotAutomaticApprovalReviewer.BuildSystemPrompt(request);
        var overrideInstructions = CopilotAutomaticApprovalReviewer.BuildSystemPrompt(
            request,
            hasExplicitUserRetryOverride: true);
        var evidence = CopilotAutomaticApprovalReviewer.BuildEvidencePrompt(
            request,
            tool,
            action,
            string.Empty,
            "Complete command: Write-Output safe");

        Assert.DoesNotContain("Explicit user retry override", ordinaryInstructions, StringComparison.Ordinal);
        Assert.Contains("# Explicit user retry override", overrideInstructions, StringComparison.Ordinal);
        Assert.Contains("not as automatic approval", overrideInstructions, StringComparison.Ordinal);
        Assert.Contains("deny again", overrideInstructions, StringComparison.Ordinal);
        Assert.DoesNotContain("Explicit user retry override", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(action.ActionId, overrideInstructions, StringComparison.Ordinal);
        Assert.DoesNotContain(action.ArgumentsDigest, overrideInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void DeniedActionCheckpointResumeRequiresTheNarrowRetryShape()
    {
        Assert.True(new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.RetryDeniedAction,
            PreviousStopReason = CopilotAgentStopReason.ApprovalDenied,
            ToolName = "RunShellCommand",
        }.IsStructurallyValid());
        Assert.True(new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.RetryDeniedAction,
            PreviousStopReason = CopilotAgentStopReason.Completed,
            ToolName = "RunShellCommand",
        }.IsStructurallyValid());
        Assert.False(new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.Resume,
            PreviousStopReason = CopilotAgentStopReason.ApprovalDenied,
        }.IsStructurallyValid());
        Assert.False(new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.RetryDeniedAction,
            PreviousStopReason = CopilotAgentStopReason.ApprovalDenied,
        }.IsStructurallyValid());
    }

    private static ConfirmableAction CreateDeniedAction(
        string actionId,
        string conversationId,
        string taskId,
        string workspacePath,
        string toolName,
        string argumentsDigest,
        DateTimeOffset? deniedAtUtc = null,
        string decisionSource = "automatic-review")
    {
        return new ConfirmableAction
        {
            ActionId = actionId,
            ToolName = toolName,
            ArgumentsDigest = argumentsDigest,
            AgentCallId = "call-" + taskId,
            ResumesAgentOnApproval = true,
            RequestContext = new CopilotConfirmationRequestContext
            {
                SourceKind = CopilotApprovalSourceKind.InAppAgent,
                ConversationId = conversationId,
                TaskId = taskId,
                WorkspacePath = workspacePath,
            },
            Status = ConfirmableActionStatus.Rejected,
            ApprovalDecisionSource = decisionSource,
            CompletedAt = deniedAtUtc ?? DateTimeOffset.UtcNow,
        };
    }

    private static ConfirmableAction CreatePendingAction(
        CopilotAgentRequest request,
        string toolName,
        string argumentsDigest)
    {
        return new ConfirmableAction
        {
            ActionId = "retry-" + Guid.NewGuid().ToString("N"),
            ToolName = toolName,
            ArgumentsDigest = argumentsDigest,
            AgentCallId = "call-" + Guid.NewGuid().ToString("N"),
            ResumesAgentOnApproval = true,
            RequestContext = new CopilotConfirmationRequestContext
            {
                SourceKind = CopilotApprovalSourceKind.InAppAgent,
                ConversationId = request.ConversationId,
                TaskId = request.TaskId,
                WorkspacePath = request.WorkspacePath,
            },
        };
    }

    private static CopilotAgentRequest CreateRequest(
        string conversationId,
        string taskId,
        string workspacePath,
        CopilotCodexApprovalsReviewer approvalsReviewer = CopilotCodexApprovalsReviewer.Unspecified)
    {
        return new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = taskId,
            WorkspacePath = workspacePath,
            UserText = "Run the exact action again.",
            TaskIntentText = "Run the exact action again.",
            Profile = CopilotProfileConfig.CreateDefault(),
            CodexApprovalsReviewer = approvalsReviewer,
        };
    }
}
