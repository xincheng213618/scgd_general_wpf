using ColorVision.Copilot;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotComposerAccessAndReferenceTests
{
    [Fact]
    public void InProgressTaskLedgerDoesNotClaimTheAgentHasStopped()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem { Id = 1, Title = "执行脚本", IsComplete = false },
                ],
            },
            AgentStopReason = CopilotAgentStopReason.None,
            IsExecutionInProgress = true,
        };

        Assert.Equal("任务执行中", message.AgentStopReasonLabel);
    }

    [Fact]
    public void ConversationAccessModeUpdatesLiveAgentContextAndRoundTrips()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");

        Assert.Equal(CopilotAgentAccessMode.FullAccess, conversation.AccessMode);
        Assert.True(conversation.AccessContext.AllowsUnattendedProtectedActions);

        conversation.AccessMode = CopilotAgentAccessMode.ConfirmProtectedActions;

        Assert.False(conversation.AccessContext.AllowsUnattendedProtectedActions);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(
            JsonConvert.SerializeObject(conversation));
        Assert.NotNull(restored);
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, restored.AccessMode);
        Assert.False(restored.AccessContext.AllowsUnattendedProtectedActions);
    }

    [Fact]
    public void FullAccessApprovalDoesNotRequireAConfirmationStoreAction()
    {
        var coordinator = new CopilotFrameworkApprovalCoordinator();

        Assert.True(coordinator.BeginIfRequired(string.Empty));
    }

    [Fact]
    public void FullAccessAutoApprovesProtectedToolsOutsideReviewMode()
    {
        var access = new CopilotAgentAccessContext(CopilotAgentAccessMode.FullAccess);
        var protectedTool = new TestTool(CopilotToolAccess.Write, CopilotToolApprovalMode.Always);
        var readTool = new TestTool(CopilotToolAccess.ReadOnly, CopilotToolApprovalMode.Never);

        Assert.True(CopilotAgentAccessPolicy.CanAutoApprove(
            new CopilotAgentRequest { Mode = CopilotAgentMode.Auto, AccessContext = access },
            protectedTool));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            new CopilotAgentRequest { Mode = CopilotAgentMode.Review, AccessContext = access },
            protectedTool));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            new CopilotAgentRequest { Mode = CopilotAgentMode.Auto, AccessContext = access },
            readTool));
    }

    [Theory]
    [InlineData("@", "")]
    [InlineData("请检查 @SFR", "SFR")]
    [InlineData("请打开 @模板 管理", "模板 管理")]
    public void ParsesTrailingComposerMention(string input, string expectedQuery)
    {
        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(input, out var mention));
        Assert.Equal(expectedQuery, mention.Query);
    }

    [Theory]
    [InlineData("mail@example.com")]
    [InlineData("请查看 @[SFR 模板] ")]
    [InlineData("第一行 @模板\n第二行")]
    public void IgnoresNonComposerMentions(string input)
    {
        Assert.False(CopilotComposerReferenceCatalog.TryParseMention(input, out _));
    }

    [Fact]
    public void CompletesMentionAsClosedToken()
    {
        Assert.True(CopilotComposerReferenceCatalog.TryParseMention("请检查 @sf", out var mention));

        var completed = CopilotComposerReferenceCatalog.CompleteMention("请检查 @sf", mention, "SFR 模板");

        Assert.Equal("请检查 @[SFR 模板] ", completed);
        Assert.False(CopilotComposerReferenceCatalog.TryParseMention(completed, out _));
    }

    [Theory]
    [InlineData("", 0, 0, "@", 1)]
    [InlineData("请检查", 3, 0, "请检查 @", 5)]
    [InlineData("请检查目标", 3, 2, "请检查 @", 5)]
    [InlineData("前后", 1, 0, "前 @ 后", 3)]
    [InlineData("前 后", 2, 0, "前 @ 后", 3)]
    public void InsertsMentionAtComposerSelection(
        string input,
        int selectionStart,
        int selectionLength,
        string expected,
        int expectedCaretIndex)
    {
        var result = CopilotComposerReferenceCatalog.InsertMention(
            input,
            selectionStart,
            selectionLength,
            out var caretIndex);

        Assert.Equal(expected, result);
        Assert.Equal(expectedCaretIndex, caretIndex);
        Assert.Equal('@', result[caretIndex - 1]);
    }

    [Fact]
    public void InsertMentionKeepsExistingActiveMention()
    {
        const string input = "请检查 @SFR";

        var result = CopilotComposerReferenceCatalog.InsertMention(
            input,
            selectionStart: 0,
            selectionLength: 0,
            out var caretIndex);

        Assert.Equal(input, result);
        Assert.Equal(input.Length, caretIndex);
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    public void ReferenceCompletionKeysWaitForUsableSearchState(
        bool isTabKey,
        bool hasSuggestions,
        bool isSearchPending,
        bool expected)
    {
        Assert.Equal(
            expected,
            CopilotComposerReferenceCatalog.ShouldConsumeReferenceCompletionKey(
                isTabKey,
                hasSuggestions,
                isSearchPending));
    }

    [Fact]
    public void WorkspaceFileSearchSkipsBuildOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-reference-" + Guid.NewGuid().ToString("N"));
        var sourceDirectory = Path.Combine(root, "Source");
        var buildDirectory = Path.Combine(root, "bin");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(buildDirectory);
        var expectedPath = Path.Combine(sourceDirectory, "TemplateEditor.xaml");
        File.WriteAllText(expectedPath, "<Grid />");
        File.WriteAllText(Path.Combine(buildDirectory, "TemplateEditor.g.cs"), "generated");

        try
        {
            var matches = CopilotComposerReferenceCatalog.SearchWorkspaceFiles(root, "TemplateEditor");

            Assert.Contains(expectedPath, matches, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(matches, path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NewComposerMentionRefreshesWorkspaceFileIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-reference-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var firstPath = Path.Combine(root, "FirstReference.cs");
        var addedPath = Path.Combine(root, "AddedAfterIndex.cs");
        File.WriteAllText(firstPath, "class FirstReference {}");

        try
        {
            var initial = await CopilotComposerReferenceCatalog.SearchWorkspaceReferencesAsync(
                root,
                "FirstReference",
                refreshIndex: true,
                CancellationToken.None);
            Assert.Contains(initial, item =>
                item.Kind == CopilotComposerReferenceKind.File
                && string.Equals(item.Value, firstPath, StringComparison.OrdinalIgnoreCase));

            File.WriteAllText(addedPath, "class AddedAfterIndex {}");
            var cached = await CopilotComposerReferenceCatalog.SearchWorkspaceReferencesAsync(
                root,
                "AddedAfterIndex",
                refreshIndex: false,
                CancellationToken.None);
            Assert.DoesNotContain(cached, item =>
                string.Equals(item.Value, addedPath, StringComparison.OrdinalIgnoreCase));

            var refreshed = await CopilotComposerReferenceCatalog.SearchWorkspaceReferencesAsync(
                root,
                "AddedAfterIndex",
                refreshIndex: true,
                CancellationToken.None);
            Assert.Contains(refreshed, item =>
                item.Kind == CopilotComposerReferenceKind.File
                && string.Equals(item.Value, addedPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestTool(
        CopilotToolAccess access,
        CopilotToolApprovalMode approvalMode) : ICopilotTool
    {
        public string Name => "TestTool";

        public string Description => "test";

        public CopilotToolAccess Access => access;

        public CopilotToolApprovalMode ApprovalMode => approvalMode;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true });
    }
}
