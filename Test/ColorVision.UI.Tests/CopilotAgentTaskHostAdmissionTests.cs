#pragma warning disable CA1707
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskHostAdmissionTests
{
    [Fact]
    public void IdleHost_AcceptsConfiguredConversationUntilShutdown()
    {
        var host = new CopilotAgentTaskHost();

        Assert.Equal(CopilotRequestAdmissionReason.MissingConversation, host.EvaluateRequestAdmission(null, CopilotAgentMode.Auto).Reason);
        Assert.Equal(CopilotRequestAdmissionReason.MissingConversation, host.EvaluateRequestAdmission(" ", CopilotAgentMode.Auto).Reason);
        Assert.True(host.EvaluateRequestAdmission("conversation-1", CopilotAgentMode.Chat).IsAllowed);
        Assert.True(host.EvaluateRequestAdmission("conversation-1", CopilotAgentMode.Auto).IsAllowed);

        host.Shutdown();

        Assert.Equal(CopilotRequestAdmissionReason.HostShutdown, host.EvaluateRequestAdmission("conversation-1", CopilotAgentMode.Auto).Reason);
    }

    [Fact]
    public async Task ActiveChat_IsExclusiveAcrossConversations()
    {
        var host = new CopilotAgentTaskHost();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = host.Start("conversation-1", CopilotAgentMode.Chat, _ => release.Task);

        Assert.Equal(CopilotRequestAdmissionReason.ConversationAlreadyScheduled, host.EvaluateRequestAdmission("conversation-1", CopilotAgentMode.Chat).Reason);
        Assert.Equal(CopilotRequestAdmissionReason.ActiveChatIsExclusive, host.EvaluateRequestAdmission("conversation-2", CopilotAgentMode.Auto).Reason);

        release.TrySetResult();
        await active.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(host.EvaluateRequestAdmission("conversation-2", CopilotAgentMode.Auto).IsAllowed);
    }

    [Fact]
    public async Task ActiveAgent_AllowsOnlyDifferentNonChatConversationWithinQueueCapacity()
    {
        var host = new CopilotAgentTaskHost(maxQueuedRuns: 1);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = host.Start("conversation-1", CopilotAgentMode.Auto, _ => release.Task);

        Assert.Equal(CopilotRequestAdmissionReason.ConversationAlreadyScheduled, host.EvaluateRequestAdmission("conversation-1", CopilotAgentMode.Auto).Reason);
        Assert.Equal(CopilotRequestAdmissionReason.ChatCannotQueue, host.EvaluateRequestAdmission("conversation-2", CopilotAgentMode.Chat).Reason);
        Assert.True(host.EvaluateRequestAdmission("conversation-2", CopilotAgentMode.Diagnose).IsAllowed);
        Assert.True(host.TrySchedule("conversation-2", CopilotAgentMode.Diagnose, _ => Task.CompletedTask, out var queued));
        Assert.Equal(CopilotRequestAdmissionReason.QueueFull, host.EvaluateRequestAdmission("conversation-3", CopilotAgentMode.Auto).Reason);

        release.TrySetResult();
        await Task.WhenAll(active.Completion, queued!.Completion).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
