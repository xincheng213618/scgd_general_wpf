using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotLocalCommandArgumentSuggestionTests
{
    [Fact]
    public void DeclaredArgumentMetadataIsCompleteAndUnambiguous()
    {
        foreach (var command in CopilotLocalCommandCatalog.All.Where(item => item.Arguments != null))
        {
            Assert.True(command.AcceptsArguments);
            Assert.NotEmpty(command.Arguments!);
            Assert.Equal(
                command.Arguments!.Count,
                command.Arguments.Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(command.Arguments, argument =>
            {
                Assert.False(string.IsNullOrWhiteSpace(argument.Value));
                Assert.False(string.IsNullOrWhiteSpace(argument.Description));
            });
        }
    }

    [Theory]
    [InlineData("/permissions ", "/permissions status", "/permissions ask", "/permissions auto")]
    [InlineData("/stats ", "/stats 7", "/stats 30", "/stats all")]
    [InlineData("/diff ", "/diff both", "/diff staged", "/diff unstaged")]
    public void StaticArgumentSuggestionsComeFromCommandMetadata(
        string input,
        params string[] expected)
    {
        var suggestions = CopilotLocalCommandCatalog.Suggest(input);

        Assert.Equal(expected, suggestions.Select(item => item.Name));
        Assert.All(suggestions, item => Assert.StartsWith("参数 · ", item.Description));
        Assert.All(suggestions, item => Assert.False(item.AcceptsArguments));
    }

    [Fact]
    public void ArgumentPrefixFiltersAndACompleteTerminalArgumentClosesSuggestions()
    {
        var partial = CopilotLocalCommandCatalog.Suggest("/diff st");

        Assert.Single(partial);
        Assert.Equal("/diff staged", partial[0].Name);
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/diff staged"));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/diff staged "));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/compact "));
    }

    [Fact]
    public void HelpArgumentsCoverEveryFixedCommandWithoutDuplicatingSlash()
    {
        var all = CopilotLocalCommandCatalog.Suggest("/help ");
        var filtered = CopilotLocalCommandCatalog.Suggest("/help per");
        var filteredWithSlash = CopilotLocalCommandCatalog.Suggest("/help /per");

        Assert.Equal(CopilotLocalCommandCatalog.All.Count, all.Count);
        Assert.Contains(all, item => item.Name == "/help permissions");
        Assert.Contains(all, item => item.Name == "/help help");
        Assert.DoesNotContain(all, item => item.Name.Contains("//", StringComparison.Ordinal));
        Assert.Single(filtered);
        Assert.Equal("/help permissions", filtered[0].Name);
        Assert.Single(filteredWithSlash);
        Assert.Equal("/help permissions", filteredWithSlash[0].Name);
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/help permissions"));
    }

    [Fact]
    public void GoalEditKeepsTheComposerOpenForTheNewObjective()
    {
        var edit = Assert.Single(CopilotLocalCommandCatalog.Suggest("/goal ed"));

        Assert.Equal("/goal edit", edit.Name);
        Assert.True(edit.AcceptsArguments);
        Assert.True(edit.RequiresMoreInputAfterCompletion);
        Assert.Equal("/goal edit ", edit.CompletionText);
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/goal edit "));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/goal pause"));
    }

    [Fact]
    public void ModelSuggestionsUseUniqueLabelsAndMarkTheCurrentProfile()
    {
        var primary = CreateProfile("profile-primary", "Primary", CopilotVendorType.DeepSeek);
        var backup = CreateProfile("profile-backup", "Backup", CopilotVendorType.Xiaomi);

        var suggestions = CopilotLocalCommandCatalog.Suggest(
            "/model ",
            profiles: [primary, backup],
            selectedProfile: primary);

        Assert.Equal(["/model Primary", "/model Backup"], suggestions.Select(item => item.Name));
        Assert.Contains("当前", suggestions[0].Description);
        Assert.DoesNotContain("当前", suggestions[1].Description);
        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/model Primary",
            profiles: [primary, backup],
            selectedProfile: primary));
    }

    [Fact]
    public void DuplicateModelLabelsFallBackToStableProfileIds()
    {
        var first = CreateProfile("profile-a", "Shared", CopilotVendorType.DeepSeek);
        var second = CreateProfile("profile-b", "Shared", CopilotVendorType.Xiaomi);

        var suggestions = CopilotLocalCommandCatalog.Suggest(
            "/model ",
            profiles: [first, second],
            selectedProfile: first);

        Assert.Equal(["/model profile-a", "/model profile-b"], suggestions.Select(item => item.Name));
    }

    [Fact]
    public void ReasoningSuggestionsFollowTheSelectedProfilesDeclaredOptions()
    {
        var profile = CreateProfile("deepseek", "DeepSeek", CopilotVendorType.DeepSeek);
        profile.ReasoningMode = CopilotReasoningMode.High;

        var suggestions = CopilotLocalCommandCatalog.Suggest(
            "/reasoning ",
            selectedProfile: profile);
        var filteredAlias = CopilotLocalCommandCatalog.Suggest(
            "/effort h",
            selectedProfile: profile);

        Assert.Equal(
            ["/reasoning auto", "/reasoning off", "/reasoning high", "/reasoning max"],
            suggestions.Select(item => item.Name));
        Assert.DoesNotContain(suggestions, item => item.Name == "/reasoning on");
        Assert.Contains("当前", suggestions.Single(item => item.Name == "/reasoning high").Description);
        Assert.Single(filteredAlias);
        Assert.Equal("/effort high", filteredAlias[0].Name);
    }

    [Fact]
    public void ProfilesWithoutReasoningControlsDoNotInventArguments()
    {
        var profile = CreateProfile("openai", "OpenAI", CopilotVendorType.OpenAI);

        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/reasoning ",
            selectedProfile: profile));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/model "));
    }

    private static CopilotProfileConfig CreateProfile(
        string id,
        string name,
        CopilotVendorType vendor)
    {
        return new CopilotProfileConfig
        {
            Id = id,
            Name = name,
            Model = name + "-model",
            VendorType = vendor,
        };
    }
}
