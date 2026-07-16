using ColorVision.Copilot;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentRequestFactoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Agent-Request-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PrepareBuildsHostContextAndLeastPrivilegePathPlan()
    {
        var solutionRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "solution")).FullName;
        var sourceRoot = Directory.CreateDirectory(Path.Combine(solutionRoot, "src")).FullName;
        var explicitDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "explicit-directory")).FullName;
        var explicitFileRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "explicit-file-root")).FullName;
        var attachmentRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "attachment-root")).FullName;
        var activeDocument = Path.Combine(sourceRoot, "active.cs");
        var explicitFile = Path.Combine(explicitFileRoot, "explicit.md");
        var attachmentFile = Path.Combine(attachmentRoot, "reference.txt");
        File.WriteAllText(activeDocument, "// active");
        File.WriteAllText(explicitFile, "explicit");
        File.WriteAllText(attachmentFile, "attachment");
        File.WriteAllText(Path.Combine(solutionRoot, "AGENTS.md"), "# Test instructions");
        var attachment = new CopilotAttachmentItem
        {
            Id = "attachment-1",
            Type = CopilotAttachmentType.File,
            Title = "Reference",
            Value = attachmentFile,
            Source = "test",
        };
        var liveItems = new List<CopilotContextItem> { new() { Id = "live-1", Title = "Live item" } };
        var liveContext = new CopilotLiveContext
        {
            SourceId = "flow-editor",
            Title = "Flow editor",
            Summary = "A node is selected.",
            SnapshotItems = liveItems,
        };
        var hostContext = new CopilotAgentHostContextSnapshot(activeDocument, solutionRoot, [attachment], liveContext);
        attachment.Value = Path.Combine(_tempRoot, "mutated.txt");
        liveItems.Add(new CopilotContextItem { Id = "live-2", Title = "Later item" });

        var plan = CopilotAgentRequestFactory.Prepare(
            $"  diagnose \"{explicitDirectory}\" and \"{explicitFile}\"  ",
            CopilotAgentMode.Diagnose,
            hostContext);

        Assert.Equal($"diagnose \"{explicitDirectory}\" and \"{explicitFile}\"", plan.UserText);
        Assert.Equal(CopilotContextScope.Diagnose, plan.ContextRequest.Scope);
        Assert.Equal(solutionRoot, plan.ContextRequest.SolutionDirectoryPath);
        Assert.Equal(activeDocument, plan.ActiveDocumentPath);
        Assert.Contains(explicitDirectory, plan.ReadableLocalDirectoryPaths);
        Assert.Contains(explicitFile, plan.ReadableLocalFilePaths);
        Assert.Equal([solutionRoot], plan.WritableLocalRootPaths);
        Assert.Contains(activeDocument, plan.WritableLocalFilePaths);
        Assert.Contains(explicitFile, plan.WritableLocalFilePaths);
        Assert.False(plan.PreferBatchReadLocalFiles);
        Assert.Contains(solutionRoot, plan.SearchRootPaths);
        Assert.Contains(explicitDirectory, plan.SearchRootPaths);
        Assert.Contains(explicitFileRoot, plan.SearchRootPaths);
        Assert.Contains(attachmentRoot, plan.SearchRootPaths);
        Assert.Equal(attachmentFile, Assert.Single(plan.Attachments).Value);
        Assert.Equal("Flow editor", hostContext.LiveContext!.Title);
        Assert.Single(hostContext.LiveContext.SnapshotItems);
        Assert.Contains(plan.ProjectInstructions, document => document.Path == Path.Combine(solutionRoot, "AGENTS.md"));
    }

    [Fact]
    public void PreparePrefersBatchReadForDirectoryOnlyAndMapsChatScope()
    {
        var directory = Directory.CreateDirectory(Path.Combine(_tempRoot, "directory-only")).FullName;
        var hostContext = new CopilotAgentHostContextSnapshot(string.Empty, string.Empty, []);

        var plan = CopilotAgentRequestFactory.Prepare($"inspect \"{directory}\"", CopilotAgentMode.Chat, hostContext);

        Assert.Equal(CopilotContextScope.Chat, plan.ContextRequest.Scope);
        Assert.Equal([directory], plan.ReadableLocalDirectoryPaths);
        Assert.Empty(plan.ReadableLocalFilePaths);
        Assert.True(plan.PreferBatchReadLocalFiles);
        Assert.Empty(plan.ProjectInstructions);
        Assert.Empty(plan.WritableLocalRootPaths);
    }

    [Fact]
    public void CreateSnapshotsRuntimeSettingsCollectionsAndEnabledMcpServers()
    {
        var profile = CreateProfile();
        var history = new List<CopilotRequestMessage> { new("user", "earlier") };
        var contextItems = new List<CopilotContextItem> { new() { Id = "context-1", Title = "Context" } };
        var defaults = new CopilotAgentDefaultsConfig
        {
            ContextWindowTokens = 64_000,
            RequestTokenBudget = 12_000,
            MaxToolCalls = 17,
            MaxAgentPasses = 9,
            TimeoutSeconds = 321,
            PreferredShell = CopilotShellKind.PowerShell,
            SkillOverrides = new ObservableCollection<CopilotAgentSkillOverrideConfig>
            {
                new() { Name = "review", State = CopilotAgentSkillOverrideState.NameOnly },
            },
        };
        var enabledServer = new CopilotMcpClientServerConfig
        {
            Name = "enabled",
            Endpoint = "https://mcp.example.test",
            Enabled = true,
            ToolRules = new ObservableCollection<CopilotMcpClientToolRule>
            {
                new() { ToolName = "inspect", AccessPolicy = CopilotMcpClientAccessPolicy.ReadOnly },
            },
        };
        var disabledServer = new CopilotMcpClientServerConfig
        {
            Name = "disabled",
            Endpoint = "https://disabled.example.test",
            Enabled = false,
        };
        var checkpoint = new CopilotAgentSessionCheckpoint();
        var recovery = new CopilotAgentRecoveryRequest { Mode = CopilotAgentRecoveryMode.Replan };
        var runControl = new CopilotAgentRunControl();
        var plan = new CopilotAgentRequestPlan
        {
            UserText = "inspect",
            Mode = CopilotAgentMode.Code,
            SearchRootPaths = ["root"],
            ReadableLocalFilePaths = ["read.cs"],
            WritableLocalRootPaths = ["write-root"],
        };

        var request = CopilotAgentRequestFactory.Create(plan, new CopilotAgentRequestBuildInput
        {
            Profile = profile,
            History = history,
            ContextItems = contextItems,
            SessionCheckpoint = checkpoint,
            Recovery = recovery,
            RunControl = runControl,
            AgentDefaults = defaults,
            ExternalMcpServers = [enabledServer, disabledServer],
        });
        history.Add(new CopilotRequestMessage("assistant", "later"));
        contextItems.Clear();
        defaults.MaxToolCalls = 99;
        defaults.PreferredShell = CopilotShellKind.CommandPrompt;
        defaults.SkillOverrides[0].State = CopilotAgentSkillOverrideState.Off;
        enabledServer.Endpoint = "https://mutated.example.test";
        enabledServer.ToolRules[0].ToolName = "mutated";

        Assert.Same(profile, request.Profile);
        Assert.Single(request.History);
        Assert.Single(request.ContextItems);
        Assert.Same(checkpoint, request.SessionCheckpoint);
        Assert.Same(recovery, request.Recovery);
        Assert.Same(runControl, request.RunControl);
        Assert.Equal(CopilotShellKind.PowerShell, request.PreferredShell);
        Assert.Equal(17, request.RunBudgetDefaults!.MaxToolCalls);
        Assert.Equal(CopilotAgentSkillOverrideState.NameOnly, request.SkillOverrides["review"]);
        var serverSnapshot = Assert.Single(request.ExternalMcpServers);
        Assert.NotSame(enabledServer, serverSnapshot);
        Assert.Equal("https://mcp.example.test", serverSnapshot.Endpoint);
        Assert.Equal("inspect", Assert.Single(serverSnapshot.ToolRules).ToolName);
    }

    [Fact]
    public void CreateDropsRecoveryWithoutCheckpoint()
    {
        var request = CopilotAgentRequestFactory.Create(new CopilotAgentRequestPlan(), new CopilotAgentRequestBuildInput
        {
            Profile = CreateProfile(),
            Recovery = new CopilotAgentRecoveryRequest { Mode = CopilotAgentRecoveryMode.Replan },
        });

        Assert.Null(request.Recovery);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
        };
    }
}
