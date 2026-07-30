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
    public void ConversationDefaultsToConfirmAndTemporaryGrantDoesNotRoundTrip()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var workspacePath = Path.Combine(Path.GetTempPath(), "copilot-access-workspace");

        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, conversation.AccessMode);
        Assert.False(conversation.AccessContext.AllowsUnattendedProtectedActions);

        conversation.PrepareFullAccessGrant(
            workspacePath,
            taskId: null,
            DateTimeOffset.UtcNow.AddMinutes(15));

        Assert.Equal(CopilotAgentAccessMode.FullAccess, conversation.AccessMode);
        Assert.True(conversation.IsFullAccessPreparedForNextTask);
        Assert.False(conversation.AccessContext.AllowsUnattendedProtectedActions);
        var serialized = JsonConvert.SerializeObject(conversation);
        Assert.DoesNotContain("\"AccessMode\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"FullAccessTaskId\"", serialized, StringComparison.Ordinal);

        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(serialized);
        Assert.NotNull(restored);
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, restored.AccessMode);
        Assert.False(restored.AccessContext.AllowsUnattendedProtectedActions);
    }

    [Fact]
    public void LegacyPersistedFullAccessRestoresAsConfirm()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var serialized = JsonConvert.SerializeObject(conversation);
        var legacyDocument = serialized.TrimEnd('}') + ",\"AccessMode\":\"FullAccess\"}";

        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(legacyDocument);

        Assert.NotNull(restored);
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, restored.AccessMode);
        Assert.False(restored.AccessContext.AllowsUnattendedProtectedActions);
        Assert.True(restored.EnsureValid());
    }

    [Fact]
    public void PreparedFullAccessOnlyAutoApprovesAnExactlyBoundTaskScope()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var workspacePath = Path.Combine(Path.GetTempPath(), "copilot-access-workspace");
        var protectedTool = new TestTool(CopilotToolAccess.Write, CopilotToolApprovalMode.Always);
        var readTool = new TestTool(CopilotToolAccess.ReadOnly, CopilotToolApprovalMode.Never);
        conversation.PrepareFullAccessGrant(
            workspacePath,
            taskId: null,
            DateTimeOffset.UtcNow.AddMinutes(15));

        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-1", workspacePath),
            protectedTool,
            workspacePath));
        Assert.True(conversation.BindFullAccessGrantToTask("task-1", workspacePath));

        Assert.True(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-1", workspacePath),
            protectedTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-1", workspacePath, CopilotAgentMode.Review),
            protectedTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-1", workspacePath, CopilotAgentMode.Plan),
            protectedTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-1", workspacePath),
            readTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-2", workspacePath),
            protectedTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-1", workspacePath + "-other"),
            protectedTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(
                conversation,
                "task-1",
                workspacePath,
                conversationId: "another-conversation"),
            protectedTool,
            workspacePath));
    }

    [Fact]
    public void TemporaryGrantUsesLiveWorkspaceAndRevokesAfterWorkspaceChanges()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var workspacePath = Path.Combine(Path.GetTempPath(), "copilot-access-live-workspace");
        var otherWorkspacePath = Path.Combine(Path.GetTempPath(), "copilot-access-other-workspace");
        var protectedTool = new TestTool(
            CopilotToolAccess.Write,
            CopilotToolApprovalMode.Always,
            allowsTemporaryFullAccess: true);
        conversation.PrepareFullAccessGrant(
            workspacePath,
            "task-live-workspace",
            DateTimeOffset.UtcNow.AddMinutes(15));
        var staleRequest = CreateRequest(conversation, "task-live-workspace", workspacePath);

        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            staleRequest,
            protectedTool,
            otherWorkspacePath));
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, conversation.AccessMode);
        Assert.False(conversation.AccessContext.AllowsUnattendedProtectedActions);
    }

    [Fact]
    public void TemporaryGrantOnlyAllowsExplicitlySupportedToolsWithinWorkspace()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var workspacePath = Path.Combine(
            Path.GetTempPath(),
            $"copilot-access-contained-workspace-{Guid.NewGuid():N}");
        var containedPath = Path.Combine(workspacePath, "result", "change.json");
        var outsidePath = Path.Combine(Path.GetTempPath(), "copilot-access-outside", "change.json");
        Directory.CreateDirectory(workspacePath);
        try
        {
            conversation.PrepareFullAccessGrant(
                workspacePath,
                "task-contained-workspace",
                DateTimeOffset.UtcNow.AddMinutes(15));
            var supportedTool = new TestTool(
                CopilotToolAccess.Write,
                CopilotToolApprovalMode.Always,
                allowsTemporaryFullAccess: true);
            var unsupportedTool = new TestTool(
                CopilotToolAccess.Write,
                CopilotToolApprovalMode.Always,
                allowsTemporaryFullAccess: false);

            Assert.True(CopilotAgentAccessPolicy.CanAutoApprove(
                CreateRequest(
                    conversation,
                    "task-contained-workspace",
                    workspacePath,
                    writableLocalFilePaths: [containedPath]),
                supportedTool,
                workspacePath));
            Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
                CreateRequest(
                    conversation,
                    "task-contained-workspace",
                    workspacePath,
                    writableLocalFilePaths: [containedPath]),
                unsupportedTool,
                workspacePath));
            Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
                CreateRequest(
                    conversation,
                    "task-contained-workspace",
                    workspacePath,
                    writableLocalFilePaths: [outsidePath]),
                supportedTool,
                workspacePath));
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public void TemporaryGrantAutoReviewsOnlyUnsupportedProtectedToolsInTheExactTaskScope()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var workspacePath = Path.Combine(Path.GetTempPath(), "copilot-auto-review-workspace");
        var deterministicTool = new TestTool(
            CopilotToolAccess.Write,
            CopilotToolApprovalMode.Always,
            allowsTemporaryFullAccess: true);
        var reviewableTool = new TestTool(
            CopilotToolAccess.Write,
            CopilotToolApprovalMode.Always,
            allowsTemporaryFullAccess: false);
        conversation.PrepareFullAccessGrant(
            workspacePath,
            "task-auto-review",
            DateTimeOffset.UtcNow.AddMinutes(15));

        Assert.True(CopilotAgentAccessPolicy.CanAutoReview(
            CreateRequest(conversation, "task-auto-review", workspacePath),
            reviewableTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(
            CreateRequest(conversation, "task-auto-review", workspacePath),
            deterministicTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(
            CreateRequest(conversation, "task-auto-review", workspacePath, CopilotAgentMode.Review),
            reviewableTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(
            CreateRequest(conversation, "task-other", workspacePath),
            reviewableTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(
            CreateRequest(
                conversation,
                "task-auto-review",
                workspacePath,
                conversationId: "conversation-other"),
            reviewableTool,
            workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(
            CreateRequest(conversation, "task-auto-review", workspacePath),
            reviewableTool,
            workspacePath + "-other"));
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, conversation.AccessMode);
    }

    [Fact]
    public void OnlyPathAndHashBoundWorkspaceEnvelopeToolsAllowTemporaryApproval()
    {
        var autoApprovableToolNames = CopilotToolRegistry.CreateCoreDefaultTools()
            .Where(tool => tool.Capability.AllowsTemporaryFullAccess)
            .Select(tool => tool.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["ApplyWorkspacePatchEnvelope", "RollbackWorkspacePatchEnvelope"],
            autoApprovableToolNames);
        Assert.False(new CopilotApplyTemplatePatchTool().AllowsTemporaryFullAccess);
        Assert.False(new CopilotApplyFlowPatchTool().Capability.AllowsTemporaryFullAccess);
        Assert.False(new CopilotConvertBatchImagesTool().Capability.AllowsTemporaryFullAccess);
    }

    [Fact]
    public void TemporaryGrantLifetimeIsClampedToSafetyMaximum()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var beforeGrant = DateTimeOffset.UtcNow;

        conversation.PrepareFullAccessGrant(
            Path.Combine(Path.GetTempPath(), "copilot-access-lifetime-workspace"),
            "task-lifetime",
            beforeGrant.AddDays(1));

        Assert.NotNull(conversation.FullAccessExpiresAtUtc);
        Assert.InRange(
            conversation.FullAccessExpiresAtUtc!.Value,
            beforeGrant.AddMinutes(14),
            beforeGrant.Add(CopilotAgentAccessContext.MaximumFullAccessLifetime).AddSeconds(1));
    }

    [Fact]
    public void ExpiredFullAccessRestoresConfirmAndCannotAutoApprove()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var workspacePath = Path.Combine(Path.GetTempPath(), "copilot-expired-access-workspace");

        conversation.PrepareFullAccessGrant(
            workspacePath,
            "task-expired",
            DateTimeOffset.UtcNow);

        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, conversation.AccessMode);
        Assert.False(conversation.AccessContext.AllowsUnattendedProtectedActions);
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(
            CreateRequest(conversation, "task-expired", workspacePath),
            new TestTool(
                CopilotToolAccess.Write,
                CopilotToolApprovalMode.Always,
                allowsTemporaryFullAccess: true),
            workspacePath));
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
        CopilotToolApprovalMode approvalMode,
        bool allowsTemporaryFullAccess = true) : ICopilotTool
    {
        public string Name => "TestTool";

        public string Description => "test";

        public CopilotToolAccess Access => access;

        public CopilotToolApprovalMode ApprovalMode => approvalMode;

        public bool AllowsTemporaryFullAccess => allowsTemporaryFullAccess;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true });
    }

    private static CopilotAgentRequest CreateRequest(
        CopilotConversationRecord conversation,
        string taskId,
        string workspacePath,
        CopilotAgentMode mode = CopilotAgentMode.Auto,
        string? conversationId = null,
        IReadOnlyList<string>? writableLocalRootPaths = null,
        IReadOnlyList<string>? writableLocalFilePaths = null)
    {
        return new CopilotAgentRequest
        {
            ConversationId = conversationId ?? conversation.Id,
            TaskId = taskId,
            WorkspacePath = workspacePath,
            Mode = mode,
            AccessContext = conversation.AccessContext,
            WritableLocalRootPaths = writableLocalRootPaths ?? Array.Empty<string>(),
            WritableLocalFilePaths = writableLocalFilePaths ?? Array.Empty<string>(),
        };
    }
}
