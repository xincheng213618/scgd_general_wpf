using ColorVision.Copilot;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotComposerAccessAndReferenceTests
{
    [Fact]
    public void ConversationAccessModeUpdatesLiveAgentContextAndRoundTrips()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");

        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, conversation.AccessMode);
        Assert.False(conversation.AccessContext.AllowsUnattendedProtectedActions);

        conversation.AccessMode = CopilotAgentAccessMode.FullAccess;

        Assert.True(conversation.AccessContext.AllowsUnattendedProtectedActions);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(
            JsonConvert.SerializeObject(conversation));
        Assert.NotNull(restored);
        Assert.Equal(CopilotAgentAccessMode.FullAccess, restored.AccessMode);
        Assert.True(restored.AccessContext.AllowsUnattendedProtectedActions);
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
