using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotResponsePersonalityTests
{
    [Theory]
    [InlineData("friendly", CopilotResponsePersonality.Friendly, "友好")]
    [InlineData("PRAGMATIC", CopilotResponsePersonality.Pragmatic, "务实")]
    [InlineData(" none ", CopilotResponsePersonality.None, "无")]
    public void CommandTokensResolveToStableConversationStyles(
        string token,
        CopilotResponsePersonality expected,
        string displayName)
    {
        Assert.True(CopilotResponsePersonalitySelection.TryParse(token, out var personality));
        Assert.Equal(expected, personality);
        Assert.Equal(token.Trim().ToLowerInvariant(), CopilotResponsePersonalitySelection.GetCommandToken(personality));
        Assert.Equal(displayName, CopilotResponsePersonalitySelection.GetDisplayName(personality));
    }

    [Fact]
    public void UnknownCommandTokenDoesNotFallBackToAStyle()
    {
        Assert.False(CopilotResponsePersonalitySelection.TryParse("verbose", out var personality));
        Assert.Equal(CopilotResponsePersonality.None, personality);
    }

    [Fact]
    public void RequestProfileAddsOnlyTheSelectedStyleAndDoesNotMutateTheSource()
    {
        var source = CreateProfile();
        source.UseSystemPromptOverride("base prompt");

        var friendly = CopilotResponsePresentationGuidance.CreateRequestProfile(
            source,
            CopilotResponsePersonality.Friendly);
        var pragmatic = CopilotResponsePresentationGuidance.CreateRequestProfile(
            source,
            CopilotResponsePersonality.Pragmatic);
        var none = CopilotResponsePresentationGuidance.CreateRequestProfile(
            source,
            CopilotResponsePersonality.None);

        Assert.Equal("base prompt", source.EffectiveSystemPrompt);
        Assert.Contains("<response_personality>", friendly.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("warm, collaborative", friendly.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("outcome-first", friendly.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.Contains("outcome-first", pragmatic.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("warm, collaborative", pragmatic.EffectiveSystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("<response_personality>", none.EffectiveSystemPrompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotResponsePersonality.Friendly)]
    [InlineData(CopilotResponsePersonality.Pragmatic)]
    public void StyleInstructionCannotOverrideTaskOrSafetyBoundaries(CopilotResponsePersonality personality)
    {
        var instruction = CopilotResponsePresentationGuidance.BuildPersonalityInstruction(personality);

        Assert.Contains("default communication style", instruction, StringComparison.Ordinal);
        Assert.Contains("task scope", instruction, StringComparison.Ordinal);
        Assert.Contains("permissions", instruction, StringComparison.Ordinal);
        Assert.Contains("safety rules", instruction, StringComparison.Ordinal);
        Assert.Contains("evidence standards", instruction, StringComparison.Ordinal);
        Assert.Contains("requested output format", instruction, StringComparison.Ordinal);
        Assert.Contains("explicit user instructions", instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void ConversationStyleRoundTripsAndInvalidPersistedValueNormalizesToNone()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        Assert.Equal(CopilotResponsePersonality.None, conversation.ResponsePersonality);
        Assert.DoesNotContain(
            nameof(CopilotConversationRecord.ResponsePersonality),
            JsonConvert.SerializeObject(conversation),
            StringComparison.Ordinal);

        conversation.ResponsePersonality = CopilotResponsePersonality.Friendly;
        var serialized = JsonConvert.SerializeObject(conversation);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(serialized);

        Assert.Contains(nameof(CopilotConversationRecord.ResponsePersonality), serialized, StringComparison.Ordinal);
        Assert.NotNull(restored);
        Assert.Equal(CopilotResponsePersonality.Friendly, restored.ResponsePersonality);

        restored.ResponsePersonality = (CopilotResponsePersonality)999;
        Assert.True(restored.EnsureValid());
        Assert.Equal(CopilotResponsePersonality.None, restored.ResponsePersonality);
    }

    [Fact]
    public void PersonalityIsPartOfCheckpointIdentityAndChangingItRequiresReplan()
    {
        var profile = CreateProfile();
        var friendly = CopilotResponsePresentationGuidance.CreateRequestProfile(
            profile,
            CopilotResponsePersonality.Friendly);
        var pragmatic = CopilotResponsePresentationGuidance.CreateRequestProfile(
            profile,
            CopilotResponsePersonality.Pragmatic);
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            friendly,
            "{}",
            capabilitySnapshot,
            taskEventJournal: journal.Snapshot());
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Paused")
        {
            AgentStopReason = CopilotAgentStopReason.Paused,
        };

        var sameStyle = CopilotAgentRecoveryPolicy.Evaluate(
            message,
            checkpoint,
            friendly,
            capabilitySnapshot);
        var changedStyle = CopilotAgentRecoveryPolicy.Evaluate(
            message,
            checkpoint,
            pragmatic,
            capabilitySnapshot);

        Assert.NotNull(checkpoint);
        Assert.Equal(CopilotAgentRecoveryMode.Resume, sameStyle.Request?.Mode);
        Assert.Equal(CopilotAgentRecoveryMode.Replan, changedStyle.Request?.Mode);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            Id = "personality-test",
            VendorType = CopilotVendorType.OpenAI,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
        };
    }
}
