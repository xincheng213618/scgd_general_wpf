using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotProfileSelectionTests
{
    [Fact]
    public void ModelCommandAcceptsAnOptionalTargetAndWaitsForTheCurrentRequest()
    {
        var withoutTarget = CopilotLocalCommandCatalog.Parse("/model");
        var withTarget = CopilotLocalCommandCatalog.Parse("/model gpt-5.2-codex");

        Assert.NotNull(withoutTarget);
        Assert.Equal(CopilotLocalCommandKind.SelectModel, withoutTarget.Command.Kind);
        Assert.Empty(withoutTarget.Arguments);
        Assert.False(withoutTarget.Command.AvailableWhileAgentRuns);
        Assert.NotNull(withTarget);
        Assert.Equal("gpt-5.2-codex", withTarget.Arguments);
        Assert.False(withTarget.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/model");
    }

    [Fact]
    public void UniqueProfileTargetPrefersIdAndMatchesDisplayNameOrModel()
    {
        var byId = CreateProfile("profile-id", "Primary", "model-primary");
        var nameLooksLikeId = CreateProfile("another-id", "profile-id", "model-secondary");
        var byName = CreateProfile("named-id", "Camera Expert", "vision-model");
        var byModel = CreateProfile("model-id", "Code Expert", "gpt-5.2-codex");
        CopilotProfileConfig[] profiles = [nameLooksLikeId, byName, byModel, byId];

        Assert.Same(byId, CopilotConversationService.FindUniqueProfileTarget(profiles, "profile-id"));
        Assert.Same(byName, CopilotConversationService.FindUniqueProfileTarget(profiles, " camera EXPERT "));
        Assert.Same(byModel, CopilotConversationService.FindUniqueProfileTarget(profiles, "GPT-5.2-CODEX"));
    }

    [Fact]
    public void UniqueProfileTargetRejectsAmbiguousOrMissingTargets()
    {
        CopilotProfileConfig[] profiles =
        [
            CreateProfile("name-1", "Shared", "model-one"),
            CreateProfile("name-2", "shared", "model-two"),
            CreateProfile("model-1", "First", "shared-model"),
            CreateProfile("model-2", "Second", "SHARED-MODEL"),
        ];

        Assert.Null(CopilotConversationService.FindUniqueProfileTarget(profiles, "shared"));
        Assert.Null(CopilotConversationService.FindUniqueProfileTarget(profiles, "shared-model"));
        Assert.Null(CopilotConversationService.FindUniqueProfileTarget(profiles, "missing"));
        Assert.Null(CopilotConversationService.FindUniqueProfileTarget(profiles, "   "));
    }

    private static CopilotProfileConfig CreateProfile(string id, string name, string model)
    {
        return new CopilotProfileConfig
        {
            Id = id,
            Name = name,
            Model = model,
        };
    }
}
