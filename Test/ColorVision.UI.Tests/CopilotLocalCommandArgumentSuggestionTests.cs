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
    [InlineData("/usage ", "/usage session", "/usage daily", "/usage weekly", "/usage cumulative")]
    [InlineData("/stats ", "/stats session", "/stats daily", "/stats weekly", "/stats cumulative")]
    [InlineData("/diff ", "/diff both", "/diff staged", "/diff unstaged")]
    [InlineData("/mcp ", "/mcp verbose")]
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
        var filtered = CopilotLocalCommandCatalog.Suggest("/help perm");
        var filteredWithSlash = CopilotLocalCommandCatalog.Suggest("/help /perm");

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
    public void SubagentActionsSuggestCurrentConversationRunIdsByState()
    {
        var conversation = CreateSubagentConversation();

        var root = CopilotLocalCommandCatalog.Suggest(
            "/agents ",
            conversation: conversation);
        var show = CopilotLocalCommandCatalog.Suggest(
            "/agents show ",
            conversation: conversation);
        var stop = CopilotLocalCommandCatalog.Suggest(
            "/agents stop ",
            conversation: conversation);
        var close = CopilotLocalCommandCatalog.Suggest(
            "/agents close ",
            conversation: conversation);
        var steer = CopilotLocalCommandCatalog.Suggest(
            "/agents steer ",
            conversation: conversation);
        var filteredAlias = CopilotLocalCommandCatalog.Suggest(
            "/subagents show scout",
            conversation: conversation);

        Assert.Equal(
            ["/agents roles", "/agents runs", "/agents active", "/agents done", "/agents show", "/agents close", "/agents steer", "/agents stop"],
            root.Select(item => item.Name));
        Assert.Equal(
            ["/agents show explore-live", "/agents show scout-done"],
            show.Select(item => item.Name));
        Assert.Contains("explore · 运行中", show[0].Description);
        Assert.Contains("正在执行 ReadLocalFile", show[0].Description);
        Assert.Contains("scout · 已完成 · 有结果", show[1].Description);
        Assert.Equal(["/agents stop explore-live"], stop.Select(item => item.Name));
        Assert.Equal(["/agents close scout-done"], close.Select(item => item.Name));
        var steerRun = Assert.Single(steer);
        Assert.Equal("/agents steer explore-live", steerRun.Name);
        Assert.True(steerRun.AcceptsArguments);
        Assert.True(steerRun.RequiresMoreInputAfterCompletion);
        Assert.Equal("/agents steer explore-live ", steerRun.CompletionText);
        Assert.Equal(["/subagents show scout-done"], filteredAlias.Select(item => item.Name));
    }

    [Fact]
    public void SubagentRunSuggestionsDoNotInventTargetsOrRepeatCompletedCommands()
    {
        var conversation = CreateSubagentConversation();

        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/agents show "));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/agents stop scout",
            conversation: conversation));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/agents show scout-done",
            conversation: conversation));
        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/agents steer explore-live ",
            conversation: conversation));
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

    [Fact]
    public void MentionCommandPassesAFreeFormQueryToTheExistingReferenceCatalog()
    {
        var invocation = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/mention FlowParam"));

        Assert.Equal(CopilotLocalCommandKind.Mention, invocation.Command.Kind);
        Assert.Equal("FlowParam", invocation.Arguments);
        Assert.Equal("/mention [查询]", invocation.Command.Usage);
        Assert.Empty(CopilotLocalCommandCatalog.Suggest("/mention "));
    }

    [Fact]
    public void PersonalityCommandSuggestsOnlySupportedConversationStyles()
    {
        var invocation = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/personality pragmatic"));
        var suggestions = CopilotLocalCommandCatalog.Suggest("/personality ");

        Assert.Equal(CopilotLocalCommandKind.SelectPersonality, invocation.Command.Kind);
        Assert.Equal("pragmatic", invocation.Arguments);
        Assert.False(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(
            ["/personality friendly", "/personality pragmatic", "/personality none"],
            suggestions.Select(item => item.Name));
    }

    [Fact]
    public void ActiveRunSuggestionsOnlyIncludeExecutableFixedCommands()
    {
        var suggestions = CopilotLocalCommandCatalog.Suggest(
            "/",
            composerContext: CopilotLocalCommandComposerContext.ActiveRun);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, item => Assert.True(item.AvailableWhileAgentRuns));
        Assert.Contains(suggestions, item => item.Name == "/status");
        Assert.Contains(suggestions, item => item.Name == "/fork");
        Assert.DoesNotContain(suggestions, item => item.Name == "/model");
        Assert.DoesNotContain(suggestions, item => item.Name == "/diff");
        Assert.DoesNotContain(suggestions, item => item.Name == "/init");
        Assert.DoesNotContain(suggestions, item => item.Name == "/mention");
        Assert.DoesNotContain(suggestions, item => item.Name == "/personality");
    }

    [Fact]
    public void ActiveRunArgumentSuggestionsFollowTheirParentAvailability()
    {
        var profile = CreateProfile("profile-primary", "Primary", CopilotVendorType.DeepSeek);
        var permissions = CopilotLocalCommandCatalog.Suggest(
            "/permissions ",
            composerContext: CopilotLocalCommandComposerContext.ActiveRun);
        var help = CopilotLocalCommandCatalog.Suggest(
            "/help ",
            composerContext: CopilotLocalCommandComposerContext.ActiveRun);

        Assert.Equal(
            ["/permissions status", "/permissions ask", "/permissions auto"],
            permissions.Select(item => item.Name));
        Assert.Equal(CopilotLocalCommandCatalog.All.Count, help.Count);
        Assert.Empty(CopilotLocalCommandCatalog.Suggest(
            "/model ",
            profiles: [profile],
            selectedProfile: profile,
            composerContext: CopilotLocalCommandComposerContext.ActiveRun));
    }

    [Fact]
    public void ActiveRunKeepsDynamicSkillsAvailableForAgentInput()
    {
        var suggestions = CopilotLocalCommandCatalog.Suggest(
            "/work",
            [new CopilotAgentSkillCatalogItem("workflow", "Inspect the current workflow")],
            composerContext: CopilotLocalCommandComposerContext.ActiveRun);

        var skill = Assert.Single(suggestions);
        Assert.Equal("/workflow", skill.Name);
        Assert.Equal(CopilotLocalCommandKind.Skill, skill.Kind);
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

    private static CopilotConversationRecord CreateSubagentConversation()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Parent answer");
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateScout",
            State = CopilotToolExecutionState.Completed,
            DelegatedRoleId = "scout",
            DelegatedRunId = "scout-done",
            DelegatedAnswerText = "Verified web result.",
        });
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateExplore",
            State = CopilotToolExecutionState.Running,
            ProgressMessage = "Explore 子 Agent 正在执行 ReadLocalFile",
            DelegatedRoleId = "explore",
            DelegatedRunId = "explore-live",
        });
        conversation.Messages.Add(assistant);
        return conversation;
    }
}
