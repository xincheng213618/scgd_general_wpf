#pragma warning disable CA1707
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotSubagentRoleRegistryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-RoleRegistry-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TrustedRegistration_UpdatesExistingToolRegistryAndCapabilityCatalogUntilDisposed()
    {
        Directory.CreateDirectory(_tempRoot);
        var capabilityCatalog = new CopilotCapabilityCatalog();
        var roleRegistry = new CopilotSubagentRoleRegistry(capabilityCatalog);
        var toolRegistry = CopilotToolRegistry.CreateDefault(roleRegistry);
        var request = CreateRequest(CopilotAgentMode.Code, "Review the workspace implementation.", [_tempRoot]);
        var beforeRevision = roleRegistry.GetSnapshot().Revision;

        using (roleRegistry.RegisterTrustedPluginRole(CreateWorkspaceRegistration()))
        {
            var snapshot = roleRegistry.GetSnapshot();
            var role = snapshot.GetRequired("reviewer");
            var tool = Assert.Single(toolRegistry.FindTools(request), candidate => candidate.Name == "DelegateReviewer");
            var capability = Assert.Single(capabilityCatalog.GetSnapshot().Capabilities);

            Assert.Equal(beforeRevision + 1, snapshot.Revision);
            Assert.Equal("sample.plugin", role.SourceId);
            Assert.Equal("2.4.1", role.SourceVersion);
            Assert.Equal(64, role.CapabilityFingerprint.Length);
            Assert.All(role.CapabilityFingerprint, character => Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
            Assert.Equal(CopilotSubagentReadCapabilities.GrepText | CopilotSubagentReadCapabilities.ReadLocalFile, role.ReadCapabilities);
            Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
            Assert.Equal(CopilotToolApprovalMode.Never, tool.Capability.ApprovalMode);
            Assert.Equal("subagent:sample.plugin:reviewer", capability.Id);
            Assert.Equal(CopilotCapabilitySourceKind.Plugin, capability.SourceKind);
            Assert.Equal("DelegateReviewer", capability.Name);
            Assert.Equal(64, capability.Fingerprint.Length);
        }

        Assert.DoesNotContain(toolRegistry.FindTools(request), candidate => candidate.Name == "DelegateReviewer");
        Assert.Empty(capabilityCatalog.GetSnapshot().Capabilities);
        Assert.Equal(beforeRevision + 2, roleRegistry.GetSnapshot().Revision);
    }

    [Fact]
    public void RoleFingerprint_ChangesWithSourceVersionAndCatalogRevisionTracksReplacement()
    {
        var capabilityCatalog = new CopilotCapabilityCatalog();
        var roleRegistry = new CopilotSubagentRoleRegistry(capabilityCatalog);
        var profile = CreateRequest(CopilotAgentMode.Code, "Review", Array.Empty<string>()).Profile;
        string firstFingerprint;
        long firstCapabilityRevision;
        CopilotAgentSessionCheckpoint checkpoint;
        using (roleRegistry.RegisterTrustedPluginRole(CreateWorkspaceRegistration("2.4.1")))
        {
            firstFingerprint = roleRegistry.GetSnapshot().GetRequired("reviewer").CapabilityFingerprint;
            var firstSnapshot = capabilityCatalog.GetSnapshot();
            firstCapabilityRevision = Assert.Single(firstSnapshot.Capabilities).Revision;
            checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(profile, "{}", firstSnapshot));
        }

        using (roleRegistry.RegisterTrustedPluginRole(CreateWorkspaceRegistration("2.5.0")))
        {
            var secondFingerprint = roleRegistry.GetSnapshot().GetRequired("reviewer").CapabilityFingerprint;
            var capability = Assert.Single(capabilityCatalog.GetSnapshot().Capabilities);

            Assert.NotEqual(firstFingerprint, secondFingerprint);
            Assert.True(capability.Revision > firstCapabilityRevision);
            var compatibility = checkpoint.EvaluateFor(profile, capabilityCatalog.GetSnapshot());
            Assert.Equal(CopilotAgentCheckpointCompatibilityKind.CapabilityDrift, compatibility.Kind);
            Assert.Contains("subagent:sample.plugin:reviewer", compatibility.ChangedCapabilityIds);
        }
    }

    [Fact]
    public void Registration_RejectsBuiltInOverridesMixedTrustDomainsAndUnsafeBudgets()
    {
        var roleRegistry = new CopilotSubagentRoleRegistry();
        var builtInOverride = CopyRegistration(CreateWorkspaceRegistration(), roleId: "explore", toolName: "DelegateReplacement");
        Assert.Throws<InvalidOperationException>(() => roleRegistry.RegisterTrustedPluginRole(builtInOverride));

        var mixed = CopyRegistration(
            CreateWorkspaceRegistration(),
            capabilities: CopilotSubagentReadCapabilities.ReadLocalFile | CopilotSubagentReadCapabilities.WebSearch);
        Assert.Throws<ArgumentException>(() => roleRegistry.RegisterTrustedPluginRole(mixed));

        var excessive = CopyRegistration(CreateWorkspaceRegistration(), maximumToolCalls: 13);
        Assert.Throws<ArgumentOutOfRangeException>(() => roleRegistry.RegisterTrustedPluginRole(excessive));

        using var first = roleRegistry.RegisterTrustedPluginRole(CreateWorkspaceRegistration("2.4.1"));
        var mixedSourceVersion = CopyRegistration(
            CreateWorkspaceRegistration("2.5.0"),
            roleId: "auditor",
            toolName: "DelegateAuditor");
        Assert.Throws<InvalidOperationException>(() => roleRegistry.RegisterTrustedPluginRole(mixedSourceVersion));
    }

    [Fact]
    public void Registration_SnapshotsMutableParentModeCollections()
    {
        Directory.CreateDirectory(_tempRoot);
        var parentModes = new List<CopilotAgentMode> { CopilotAgentMode.Code };
        var registration = CopyRegistration(CreateWorkspaceRegistration(), parentModes: parentModes);
        var roleRegistry = new CopilotSubagentRoleRegistry();
        var toolRegistry = CopilotToolRegistry.CreateDefault(roleRegistry);
        using var handle = roleRegistry.RegisterTrustedPluginRole(registration);

        parentModes.Clear();
        parentModes.Add(CopilotAgentMode.Chat);

        var role = roleRegistry.GetSnapshot().GetRequired("reviewer");
        Assert.Equal([CopilotAgentMode.Code], role.ParentModes);
        Assert.Contains(toolRegistry.FindTools(CreateRequest(CopilotAgentMode.Code, "Review", [_tempRoot])), tool => tool.Name == "DelegateReviewer");
        Assert.DoesNotContain(toolRegistry.FindTools(CreateRequest(CopilotAgentMode.Chat, "Review", [_tempRoot])), tool => tool.Name == "DelegateReviewer");
    }

    [Fact]
    public async Task RegisteredWorkspaceRole_RunsInFreshContextWithOnlyDeclaredHostTools()
    {
        Directory.CreateDirectory(_tempRoot);
        using var chatClient = new CapturingAnswerChatClient();
        var roleRegistry = new CopilotSubagentRoleRegistry();
        using var registration = roleRegistry.RegisterTrustedPluginRole(CreateWorkspaceRegistration());
        var role = roleRegistry.GetSnapshot().GetRequired("reviewer");
        var runner = new CopilotSubagentRunner(_ => chatClient);
        var parent = CreateRequest(CopilotAgentMode.Code, "Review the workspace implementation.", [_tempRoot]);
        parent = new CopilotAgentRequest
        {
            UserText = parent.UserText,
            Profile = parent.Profile,
            Mode = parent.Mode,
            SearchRootPaths = parent.SearchRootPaths,
            History = [new CopilotRequestMessage("user", "PARENT_HISTORY_SECRET")],
            Attachments = [new CopilotAttachmentItem { Title = "PARENT_ATTACHMENT_SECRET", Value = "secret" }],
        };

        var result = await runner.RunAsync(parent, role, new CopilotSubagentRunRequest
        {
            RunId = "reviewer-test-run",
            Task = "Inspect error handling and report exact file evidence.",
            RequestTokenBudget = 8_000,
        }, CancellationToken.None);

        Assert.Equal("role answer", result.Answer);
        var functionNames = chatClient.LastOptions?.Tools?.Select(tool => tool.Name).OrderBy(name => name).ToArray() ?? [];
        Assert.Equal(["colorvision_grep_text", "colorvision_read_local_file"], functionNames);
        Assert.DoesNotContain(functionNames, name => name.Contains("todo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(functionNames, name => name.Contains("mode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(functionNames, name => name.Contains("skill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("PARENT_HISTORY_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("PARENT_ATTACHMENT_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.Contains("Inspect error handling", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.Contains("fresh read-only code reviewer", chatClient.LastOptions?.Instructions ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisteredPublicWebRole_DropsWorkspaceContextAndUsesOnlyDeclaredWebTools()
    {
        Directory.CreateDirectory(_tempRoot);
        using var chatClient = new CapturingAnswerChatClient();
        var roleRegistry = new CopilotSubagentRoleRegistry();
        using var registration = roleRegistry.RegisterTrustedPluginRole(CreateWebRegistration());
        var role = roleRegistry.GetSnapshot().GetRequired("standards-scout");
        var runner = new CopilotSubagentRunner(_ => chatClient);
        var parent = CreateRequest(CopilotAgentMode.Web, "Research current official standards.", [_tempRoot]);
        parent = new CopilotAgentRequest
        {
            UserText = parent.UserText,
            Profile = parent.Profile,
            Mode = parent.Mode,
            SearchRootPaths = parent.SearchRootPaths,
            ActiveDocumentPath = Path.Combine(_tempRoot, "PARENT_DOCUMENT_SECRET.cs"),
            ProjectInstructions = [new CopilotProjectInstructionDocument { Path = Path.Combine(_tempRoot, "AGENTS.md"), Content = "PARENT_PROJECT_SECRET" }],
        };

        await runner.RunAsync(parent, role, new CopilotSubagentRunRequest
        {
            RunId = "standards-test-run",
            Task = "Find current primary sources and return exact URLs.",
            RequestTokenBudget = 8_000,
        }, CancellationToken.None);

        var functionNames = chatClient.LastOptions?.Tools?.Select(tool => tool.Name).OrderBy(name => name).ToArray() ?? [];
        Assert.Equal(["colorvision_web_search"], functionNames);
        Assert.DoesNotContain("PARENT_PROJECT_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("PARENT_DOCUMENT_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain(_tempRoot, chatClient.AllMessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public standards scout", chatClient.LastOptions?.Instructions ?? string.Empty, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private static CopilotSubagentRoleRegistration CreateWorkspaceRegistration(string version = "2.4.1")
    {
        return new CopilotSubagentRoleRegistration
        {
            SourceId = "sample.plugin",
            SourceName = "Sample Plugin",
            SourceVersion = version,
            RoleId = "reviewer",
            ToolName = "DelegateReviewer",
            DisplayName = "Reviewer",
            Description = "Delegate a bounded read-only code review to a specialized child Agent.",
            RuntimeInstructions = "You are a fresh read-only code reviewer. Inspect only the requested workspace evidence and report exact paths without changing files.",
            ContextScope = CopilotSubagentContextScope.WorkspaceReadOnly,
            ReadCapabilities = CopilotSubagentReadCapabilities.GrepText | CopilotSubagentReadCapabilities.ReadLocalFile,
            ChildMode = CopilotAgentMode.Code,
            ParentModes = [CopilotAgentMode.Code, CopilotAgentMode.Diagnose],
            MaximumToolCalls = 5,
            MaximumAgentPasses = 2,
            MaximumDuration = TimeSpan.FromSeconds(60),
            MaximumAnswerCharacters = 8_000,
        };
    }

    private static CopilotSubagentRoleRegistration CreateWebRegistration()
    {
        return new CopilotSubagentRoleRegistration
        {
            SourceId = "standards.plugin",
            SourceName = "Standards Plugin",
            SourceVersion = "1.0.0",
            RoleId = "standards-scout",
            ToolName = "DelegateStandardsScout",
            DisplayName = "Standards Scout",
            Description = "Delegate bounded public standards research to a specialized read-only child Agent.",
            RuntimeInstructions = "You are a fresh public standards scout. Use only public search evidence and return exact primary source URLs.",
            ContextScope = CopilotSubagentContextScope.PublicWeb,
            ReadCapabilities = CopilotSubagentReadCapabilities.WebSearch,
            ChildMode = CopilotAgentMode.Web,
            ParentModes = [CopilotAgentMode.Auto, CopilotAgentMode.Web],
            MaximumToolCalls = 4,
            MaximumAgentPasses = 2,
            MaximumDuration = TimeSpan.FromSeconds(60),
            MaximumAnswerCharacters = 8_000,
        };
    }

    private static CopilotSubagentRoleRegistration CopyRegistration(
        CopilotSubagentRoleRegistration source,
        string? roleId = null,
        string? toolName = null,
        CopilotSubagentReadCapabilities? capabilities = null,
        int? maximumToolCalls = null,
        IReadOnlyList<CopilotAgentMode>? parentModes = null)
    {
        return new CopilotSubagentRoleRegistration
        {
            SourceId = source.SourceId,
            SourceName = source.SourceName,
            SourceVersion = source.SourceVersion,
            RoleId = roleId ?? source.RoleId,
            ToolName = toolName ?? source.ToolName,
            DisplayName = source.DisplayName,
            Description = source.Description,
            RuntimeInstructions = source.RuntimeInstructions,
            ContextScope = source.ContextScope,
            ReadCapabilities = capabilities ?? source.ReadCapabilities,
            ChildMode = source.ChildMode,
            ParentModes = parentModes ?? source.ParentModes,
            MaximumToolCalls = maximumToolCalls ?? source.MaximumToolCalls,
            MaximumAgentPasses = source.MaximumAgentPasses,
            MaximumDuration = source.MaximumDuration,
            MaximumAnswerCharacters = source.MaximumAnswerCharacters,
        };
    }

    private static CopilotAgentRequest CreateRequest(CopilotAgentMode mode, string text, IReadOnlyList<string> roots)
    {
        return new CopilotAgentRequest
        {
            UserText = text,
            Profile = new CopilotProfileConfig
            {
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "secret-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 256,
            },
            RunBudgetDefaults = new CopilotAgentRunBudgetDefaults { MaxToolCalls = 8 },
            Mode = mode,
            SearchRootPaths = roots,
        };
    }

    private sealed class CapturingAnswerChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public string AllMessageText { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Capture(messages, options);
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "role answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Capture(messages, options);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "role answer");
        }

        private void Capture(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options)
        {
            LastOptions = options;
            AllMessageText = string.Join("\n", messages
                .SelectMany(message => message.Contents)
                .OfType<TextContent>()
                .Select(content => content.Text));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
