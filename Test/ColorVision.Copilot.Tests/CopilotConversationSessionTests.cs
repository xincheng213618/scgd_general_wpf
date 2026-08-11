using ColorVision.Copilot;
using System.Collections.ObjectModel;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationSessionTests
{
    [Fact]
    public void SelectConversationUsesItsProfileInsteadOfAnUnrelatedActiveProfile()
    {
        var (config, firstProfile, secondProfile) = CreateConfig();
        var firstConversation = CreateConversation("first", firstProfile);
        var secondConversation = CreateConversation("second", secondProfile);
        var state = new CopilotChatState
        {
            Conversations = [firstConversation, secondConversation],
            ActiveConversationId = secondConversation.Id,
            ActiveProfileId = firstProfile.Id,
        };
        var session = new CopilotConversationSession(state, config);

        var result = session.SelectConversation(secondConversation);

        Assert.True(result.IsAccepted);
        Assert.Same(secondConversation, result.SelectedConversation);
        Assert.Same(secondProfile, result.SelectedProfile);
        Assert.Same(secondConversation, session.SelectedConversation);
        Assert.Same(secondProfile, session.SelectedProfile);
        Assert.Equal(secondConversation.Id, state.ActiveConversationId);
        Assert.Equal(secondProfile.Id, state.ActiveProfileId);
        Assert.True(result.StateChanged);
    }

    [Fact]
    public void CreateConversationUsesTheActiveProfileWithoutSelectingIt()
    {
        var (config, _, secondProfile) = CreateConfig();
        var state = new CopilotChatState
        {
            Conversations = [],
            ActiveProfileId = secondProfile.Id,
        };
        var session = new CopilotConversationSession(state, config);

        var conversation = session.CreateConversation();

        Assert.Same(conversation, Assert.Single(state.Conversations));
        Assert.Equal(secondProfile.Id, conversation.ProfileId);
        Assert.Null(session.SelectedConversation);
        Assert.Null(session.SelectedProfile);
        Assert.Equal(string.Empty, state.ActiveConversationId);
        Assert.Equal(secondProfile.Id, state.ActiveProfileId);
    }

    [Fact]
    public void SelectConversationReturnsTheTransitionAndSynchronizesBothStateIds()
    {
        var (config, firstProfile, secondProfile) = CreateConfig();
        var firstConversation = CreateConversation("first", firstProfile);
        var secondConversation = CreateConversation("second", firstProfile);
        var state = new CopilotChatState
        {
            Conversations = [firstConversation, secondConversation],
            ActiveConversationId = firstConversation.Id,
            ActiveProfileId = firstProfile.Id,
        };
        var session = new CopilotConversationSession(state, config);
        session.SelectConversation(firstConversation);

        var result = session.SelectConversation(secondConversation, secondProfile.Id);

        Assert.True(result.IsAccepted);
        Assert.Same(firstConversation, result.PreviousConversation);
        Assert.Same(secondConversation, result.SelectedConversation);
        Assert.Same(firstProfile, result.PreviousProfile);
        Assert.Same(secondProfile, result.SelectedProfile);
        Assert.True(result.ConversationChanged);
        Assert.True(result.ProfileChanged);
        Assert.True(result.ConversationProfileChanged);
        Assert.True(result.StateChanged);
        Assert.Equal(secondConversation.Id, state.ActiveConversationId);
        Assert.Equal(secondProfile.Id, state.ActiveProfileId);
        Assert.Equal(secondProfile.Id, secondConversation.ProfileId);
        Assert.Equal(secondProfile.DisplayLabel, secondConversation.ProfileDisplayName);
    }

    [Fact]
    public void SelectingTheSameConversationCanApplyAnExplicitPreferredProfile()
    {
        var (config, firstProfile, secondProfile) = CreateConfig();
        var conversation = CreateConversation("selected", firstProfile);
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            ActiveConversationId = conversation.Id,
            ActiveProfileId = firstProfile.Id,
        };
        var session = new CopilotConversationSession(state, config);
        session.SelectConversation(conversation);

        var result = session.SelectConversation(conversation, secondProfile.Id);

        Assert.True(result.IsAccepted);
        Assert.False(result.ConversationChanged);
        Assert.True(result.ProfileChanged);
        Assert.True(result.ConversationProfileChanged);
        Assert.True(result.StateChanged);
        Assert.Equal(secondProfile.Id, state.ActiveProfileId);
        Assert.Equal(secondProfile.Id, conversation.ProfileId);
        Assert.Equal(secondProfile.DisplayLabel, conversation.ProfileDisplayName);
    }

    [Fact]
    public void SelectConversationRejectsArchivedOrForeignRecordsWithoutChangingSelection()
    {
        var (config, firstProfile, _) = CreateConfig();
        var selectedConversation = CreateConversation("selected", firstProfile);
        var archivedConversation = CreateConversation("archived", firstProfile);
        archivedConversation.IsArchived = true;
        var foreignConversation = CreateConversation("foreign", firstProfile);
        var state = new CopilotChatState
        {
            Conversations = [selectedConversation, archivedConversation],
            ActiveConversationId = selectedConversation.Id,
            ActiveProfileId = firstProfile.Id,
        };
        var session = new CopilotConversationSession(state, config);
        session.SelectConversation(selectedConversation);

        var archivedResult = session.SelectConversation(archivedConversation);
        var foreignResult = session.SelectConversation(foreignConversation);

        Assert.False(archivedResult.IsAccepted);
        Assert.False(foreignResult.IsAccepted);
        Assert.False(archivedResult.Changed);
        Assert.False(foreignResult.Changed);
        Assert.Same(selectedConversation, session.SelectedConversation);
        Assert.Equal(selectedConversation.Id, state.ActiveConversationId);
        Assert.Equal(firstProfile.Id, state.ActiveProfileId);
    }

    [Fact]
    public void SelectProfileCanSynchronizeTheSelectedConversationExplicitly()
    {
        var (config, firstProfile, secondProfile) = CreateConfig();
        var conversation = CreateConversation("selected", firstProfile);
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            ActiveConversationId = conversation.Id,
            ActiveProfileId = firstProfile.Id,
        };
        var session = new CopilotConversationSession(state, config);
        session.SelectConversation(conversation);

        var synchronized = session.SelectProfile(secondProfile, synchronizeConversation: true);

        Assert.Same(firstProfile, synchronized.PreviousProfile);
        Assert.Same(secondProfile, synchronized.SelectedProfile);
        Assert.Same(conversation, synchronized.SelectedConversation);
        Assert.True(synchronized.ProfileChanged);
        Assert.True(synchronized.ConversationProfileChanged);
        Assert.True(synchronized.StateChanged);
        Assert.Equal(secondProfile.Id, state.ActiveProfileId);
        Assert.Equal(secondProfile.Id, conversation.ProfileId);

        var selectionOnly = session.SelectProfile(firstProfile, synchronizeConversation: false);

        Assert.True(selectionOnly.ProfileChanged);
        Assert.False(selectionOnly.ConversationProfileChanged);
        Assert.True(selectionOnly.StateChanged);
        Assert.Equal(firstProfile.Id, state.ActiveProfileId);
        Assert.Equal(secondProfile.Id, conversation.ProfileId);
    }

    [Fact]
    public void SelectingTheSameProfileCanRepairConversationProfileDrift()
    {
        var (config, firstProfile, secondProfile) = CreateConfig();
        var conversation = CreateConversation("selected", firstProfile);
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            ActiveConversationId = conversation.Id,
            ActiveProfileId = firstProfile.Id,
        };
        var session = new CopilotConversationSession(state, config);
        session.SelectConversation(conversation);
        conversation.ProfileId = secondProfile.Id;
        conversation.ProfileDisplayName = secondProfile.DisplayLabel;

        var result = session.SelectProfile(firstProfile, synchronizeConversation: true);

        Assert.False(result.ProfileChanged);
        Assert.True(result.ConversationProfileChanged);
        Assert.True(result.StateChanged);
        Assert.Equal(firstProfile.Id, conversation.ProfileId);
        Assert.Equal(firstProfile.DisplayLabel, conversation.ProfileDisplayName);
    }

    private static (CopilotConfig Config, CopilotProfileConfig First, CopilotProfileConfig Second) CreateConfig()
    {
        var first = new CopilotProfileConfig
        {
            Id = "profile-first",
            Name = "First profile",
        };
        var second = new CopilotProfileConfig
        {
            Id = "profile-second",
            Name = "Second profile",
        };
        var config = new CopilotConfig
        {
            Profiles = new ObservableCollection<CopilotProfileConfig> { first, second },
        };
        return (config, first, second);
    }

    private static CopilotConversationRecord CreateConversation(
        string id,
        CopilotProfileConfig profile)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = id;
        return conversation;
    }
}
