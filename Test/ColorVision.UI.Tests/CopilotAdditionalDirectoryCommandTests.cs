using ColorVision.Copilot;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAdditionalDirectoryCommandTests
{
    [Fact]
    public void CommandUsesAnIdleConversationScopedLifecycle()
    {
        var invocation = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse(@"/add-dir ""C:\reference repo"""));

        Assert.Equal(CopilotLocalCommandKind.AdditionalDirectories, invocation.Command.Kind);
        Assert.Equal(@"""C:\reference repo""", invocation.Arguments);
        Assert.False(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(CopilotAdditionalDirectoryCommand.Usage, invocation.Command.Usage);
        Assert.Contains(
            CopilotLocalCommandCatalog.Suggest("/add-dir "),
            suggestion => suggestion.Name == "/add-dir remove");
    }

    [Theory]
    [InlineData("", (int)CopilotAdditionalDirectoryCommandAction.List, 0)]
    [InlineData("list", (int)CopilotAdditionalDirectoryCommandAction.List, 0)]
    [InlineData("clear", (int)CopilotAdditionalDirectoryCommandAction.Clear, 0)]
    [InlineData("remove 2", (int)CopilotAdditionalDirectoryCommandAction.Remove, 2)]
    [InlineData("remove", (int)CopilotAdditionalDirectoryCommandAction.Invalid, 0)]
    public void ParserRecognizesLifecycleActions(string arguments, int expectedAction, int expectedOrdinal)
    {
        var request = CopilotAdditionalDirectoryCommand.Parse(arguments);

        Assert.Equal(expectedAction, (int)request.Action);
        Assert.Equal(expectedOrdinal, request.Ordinal);
    }

    [Fact]
    public void QuotedExistingDirectoryIsNormalizedButRelativeDirectoryIsRejected()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Assert.True(
                CopilotAdditionalDirectoryCommand.TryNormalizeExistingDirectory(
                    $"“{root}”",
                    out var normalized,
                    out var errorMessage),
                errorMessage);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), normalized);

            Assert.False(
                CopilotAdditionalDirectoryCommand.TryNormalizeExistingDirectory(
                    ".\\reference",
                    out _,
                    out errorMessage));
            Assert.Contains("绝对路径", errorMessage, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AdditionalDirectoryEntersReadScopeWithoutExpandingWriteOrTrustRoots()
    {
        var root = CreateTemporaryDirectory();
        var workspace = Directory.CreateDirectory(Path.Combine(root, "workspace")).FullName;
        var reference = Directory.CreateDirectory(Path.Combine(root, "reference")).FullName;
        try
        {
            File.WriteAllText(Path.Combine(reference, "AGENTS.md"), "Do not load from an added read root.");
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: workspace,
                attachments: null,
                additionalReadRootPaths: [reference]);

            var plan = CopilotAgentRequestFactory.Prepare(
                "比较工作区与参考目录中的代码实现。",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.Contains(workspace, plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(reference, plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([reference], plan.ReadableLocalDirectoryPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([workspace], plan.WritableLocalRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([workspace], plan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                plan.ProjectInstructions,
                document => document.Path.StartsWith(reference, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConversationPersistsAndBranchesAdditionalDirectoriesWithoutSharingTheCollection()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            source.AdditionalReadRootPaths.Add(root);
            var user = new CopilotChatMessage(CopilotChatRole.User, "Inspect the reference.");
            var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Inspection complete.");
            source.Messages.Add(user);
            source.Messages.Add(assistant);

            var serialized = JsonConvert.SerializeObject(source);
            var restored = Assert.IsType<CopilotConversationRecord>(
                JsonConvert.DeserializeObject<CopilotConversationRecord>(serialized));
            restored.EnsureValid();
            var branch = CopilotConversationBranchService.CreateBranch(
                restored,
                restored.Messages[1],
                "Reference branch");

            Assert.Contains(nameof(CopilotConversationRecord.AdditionalReadRootPaths), serialized);
            Assert.Equal([root], restored.AdditionalReadRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([root], branch.AdditionalReadRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.False(CopilotConversationService.IsReusableEmpty(restored));
            Assert.True(CopilotConversationService.IsHistory(restored));

            restored.AdditionalReadRootPaths.Clear();
            Assert.Equal([root], branch.AdditionalReadRootPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReportExplainsReadOnlyAndConfigurationDiscoveryBoundaries()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var report = CopilotAdditionalDirectoryCommand.Format([root]);

            Assert.Contains(root, report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("仅进入后续 Agent 请求的搜索与读取范围", report, StringComparison.Ordinal);
            Assert.Contains("不进入可写范围", report, StringComparison.Ordinal);
            Assert.Contains("不会成为项目指令、Skill、Hook、MCP", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ChatStateStoreRoundTripsAdditionalDirectoriesWithTheCurrentSchema()
    {
        var root = CreateTemporaryDirectory();
        var reference = Directory.CreateDirectory(Path.Combine(root, "reference")).FullName;
        var storeRoot = Path.Combine(root, "state");
        try
        {
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            conversation.AdditionalReadRootPaths.Add(reference);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            };
            var store = new CopilotChatStateStore(storeRoot);

            store.Save(state);
            var restored = new CopilotChatStateStore(storeRoot).Load();

            Assert.Equal(CopilotChatState.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Equal(
                [reference],
                Assert.Single(restored.Conversations).AdditionalReadRootPaths,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "copilot-add-dir-" + Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path).FullName;
    }
}
