using ColorVision.Copilot;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationClearTests
{
    [Fact]
    public void ClearCommandAcceptsAnOptionalPreviousTitleAndWaitsForTheCurrentRequest()
    {
        var withoutTitle = CopilotLocalCommandCatalog.Parse("/clear");
        var withTitle = CopilotLocalCommandCatalog.Parse("/clear Camera calibration");

        Assert.NotNull(withoutTitle);
        Assert.Equal(CopilotLocalCommandKind.ClearConversation, withoutTitle.Command.Kind);
        Assert.Empty(withoutTitle.Arguments);
        Assert.False(withoutTitle.Command.AvailableWhileAgentRuns);
        Assert.NotNull(withTitle);
        Assert.Equal("Camera calibration", withTitle.Arguments);
        Assert.False(withTitle.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/clear");
    }

    [Fact]
    public void FreshConversationPreservesTheNamedPreviousConversation()
    {
        var profile = new CopilotProfileConfig
        {
            Id = "profile-id",
            Name = "Primary",
        };
        var previous = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        previous.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect the workspace"));
        previous.SetCustomTitle("Camera calibration");
        var conversations = new ObservableCollection<CopilotConversationRecord> { previous };

        var fresh = CopilotConversationService.ResolveNewTarget(conversations, previous, profile);

        Assert.NotSame(previous, fresh);
        Assert.Equal("Camera calibration", previous.Title);
        Assert.Contains(previous, conversations);
        Assert.Contains(fresh, conversations);
        Assert.Empty(fresh.Messages);
        Assert.Equal(profile.Id, fresh.ProfileId);
    }

    [Fact]
    public void FreshConversationDoesNotAdoptAnotherEmptyConversationIdentity()
    {
        var profile = new CopilotProfileConfig { Id = "profile-id", Name = "Primary" };
        var previous = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        previous.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Current task"));
        var unrelatedEmpty = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        var conversations = new ObservableCollection<CopilotConversationRecord>
        {
            previous,
            unrelatedEmpty,
        };

        var fresh = CopilotConversationService.ResolveNewTarget(conversations, previous, profile);

        Assert.NotSame(previous, fresh);
        Assert.NotSame(unrelatedEmpty, fresh);
        Assert.Equal(3, conversations.Count);
    }

    [Fact]
    public void ReusableEmptyConversationRejectsHiddenIdentityContextOrAuthorization()
    {
        var clean = CreateEmptyConversation();
        var named = CreateEmptyConversation();
        named.SetCustomTitle("Keep me");
        var pinned = CreateEmptyConversation();
        pinned.IsPinned = true;
        var checkpointed = CreateEmptyConversation();
        checkpointed.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint();
        var compacted = CreateEmptyConversation();
        compacted.Compaction = new CopilotConversationCompaction();
        var branched = CreateEmptyConversation();
        branched.BranchOrigin = new CopilotConversationBranchOrigin();
        var goalBound = CreateEmptyConversation();
        goalBound.Goal = CopilotConversationGoal.Create("Keep iterating", DateTimeOffset.UtcNow);
        var measured = CreateEmptyConversation();
        measured.LastUsageTotalTokens = 1;
        var authorized = CreateEmptyConversation();
        authorized.PrepareFullAccessGrant(
            "C:\\workspace",
            taskId: null,
            expiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.True(CopilotConversationService.IsReusableEmpty(clean));
        Assert.False(CopilotConversationService.IsReusableEmpty(named));
        Assert.False(CopilotConversationService.IsReusableEmpty(pinned));
        Assert.False(CopilotConversationService.IsReusableEmpty(checkpointed));
        Assert.False(CopilotConversationService.IsReusableEmpty(compacted));
        Assert.False(CopilotConversationService.IsReusableEmpty(branched));
        Assert.False(CopilotConversationService.IsReusableEmpty(goalBound));
        Assert.False(CopilotConversationService.IsReusableEmpty(measured));
        Assert.False(CopilotConversationService.IsReusableEmpty(authorized));
    }

    private static CopilotConversationRecord CreateEmptyConversation()
    {
        return CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
    }
}
