using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotQueuedFollowUpRecoveryTests
{
    [Fact]
    public void RestoreToDraftsPreservesQueueOrderAndExistingDraft()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.DraftText = "正在编辑的草稿";
        var state = new CopilotChatState
        {
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
            {
                CreateRecord("run-1", conversation.Id, "先检查构建"),
                CreateRecord("run-2", conversation.Id, "再运行测试"),
                CreateRecord("run-orphan", "missing-conversation", "不应恢复"),
            },
        };

        Assert.True(CopilotQueuedFollowUpRecovery.RestoreToDrafts(state));

        var expected = "正在编辑的草稿" + Environment.NewLine + Environment.NewLine
            + "以下排队后续尚未执行，请检查后重新发送：" + Environment.NewLine + Environment.NewLine
            + "1. 先检查构建" + Environment.NewLine + Environment.NewLine
            + "2. 再运行测试";
        Assert.Equal(expected, conversation.DraftText);
        Assert.Equal(2, state.RecoveredQueuedFollowUpCount);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    [Fact]
    public void StateInitializationAutomaticallyRestoresQueuedFollowUps()
    {
        var config = new CopilotConfig();
        config.EnsureInitialized();
        var conversation = CopilotConversationRecord.CreateEmpty(string.Empty, string.Empty);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
            {
                CreateRecord("run-1", conversation.Id, "恢复后的草稿"),
            },
        };

        Assert.True(state.EnsureInitialized(config));

        Assert.Equal("恢复后的草稿", conversation.DraftText);
        Assert.Equal(1, state.RecoveredQueuedFollowUpCount);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    [Fact]
    public void RestoreToDraftsRecoversRequestModeAndDeduplicatesAttachments()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var attachment = CopilotAttachmentItem.CreateContext(
            "camera context",
            "Camera",
            "colorvision://camera");
        var first = CreateRecord("run-1", conversation.Id, "先检查相机");
        first.ComposerState = CopilotComposerStash.Capture(
            first.Prompt,
            first.Prompt.Length,
            CopilotAgentMode.Code,
            [attachment]);
        var second = CreateRecord("run-2", conversation.Id, "再验证恢复");
        second.ComposerState = CopilotComposerStash.Capture(
            second.Prompt,
            second.Prompt.Length,
            CopilotAgentMode.Code,
            [attachment]);
        var state = new CopilotChatState
        {
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
            {
                first,
                second,
            },
        };

        Assert.True(CopilotQueuedFollowUpRecovery.RestoreToDrafts(state));

        Assert.Contains("1. 先检查相机", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("2. 再验证恢复", conversation.DraftText, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentMode.Code, conversation.DraftRequestMode);
        var restoredAttachment = Assert.Single(conversation.Attachments);
        Assert.Equal("camera context", restoredAttachment.Value);
        Assert.NotSame(attachment, restoredAttachment);
        Assert.Equal(2, state.RecoveredQueuedFollowUpCount);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    [Fact]
    public void MixedRecoveredModesRemainVisibleAndFallBackToAuto()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var code = CreateRecord("run-code", conversation.Id, "修复代码");
        code.ComposerState = CopilotComposerStash.Capture(
            code.Prompt,
            code.Prompt.Length,
            CopilotAgentMode.Code,
            []);
        var review = CreateRecord("run-review", conversation.Id, "审查改动");
        review.ComposerState = CopilotComposerStash.Capture(
            review.Prompt,
            review.Prompt.Length,
            CopilotAgentMode.Review,
            []);
        var state = new CopilotChatState
        {
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
            {
                code,
                review,
            },
        };

        CopilotQueuedFollowUpRecovery.RestoreToDrafts(state);

        Assert.Contains("[Code] 修复代码", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("[Review] 审查改动", conversation.DraftText, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentMode.Auto, conversation.DraftRequestMode);
    }

    [Fact]
    public void ExistingDraftModeConflictKeepsRecoveredModeVisible()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.DraftText = "已有审查草稿";
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        var queuedCode = CreateRecord("run-code", conversation.Id, "修复代码");
        queuedCode.ComposerState = CopilotComposerStash.Capture(
            queuedCode.Prompt,
            queuedCode.Prompt.Length,
            CopilotAgentMode.Code,
            []);
        var state = new CopilotChatState
        {
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
            {
                queuedCode,
            },
        };

        CopilotQueuedFollowUpRecovery.RestoreToDrafts(state);

        Assert.Contains("已有审查草稿", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("[Code] 修复代码", conversation.DraftText, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
    }

    [Fact]
    public void RestoreToDraftsDropsInvalidDuplicateAndExcessRecords()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var records = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
        {
            CreateRecord("run-1", conversation.Id, "保留一次"),
            CreateRecord("run-1", conversation.Id, "重复运行编号"),
            CreateRecord("run-invalid", conversation.Id, new string('x', CopilotQueuedFollowUpRecoveryRecord.MaximumPromptCharacters + 1)),
        };
        for (var index = records.Count; index < CopilotAgentTaskHost.MaximumQueuedRuns + 2; index++)
            records.Add(CreateRecord($"run-{index}", conversation.Id, $"提示 {index}"));
        var state = new CopilotChatState
        {
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries = records,
        };

        Assert.True(CopilotQueuedFollowUpRecovery.RestoreToDrafts(state));

        Assert.Contains("保留一次", conversation.DraftText, StringComparison.Ordinal);
        Assert.DoesNotContain("重复运行编号", conversation.DraftText, StringComparison.Ordinal);
        Assert.DoesNotContain($"提示 {CopilotAgentTaskHost.MaximumQueuedRuns}", conversation.DraftText, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentTaskHost.MaximumQueuedRuns - 2, state.RecoveredQueuedFollowUpCount);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    [Fact]
    public void StateStoreRoundTripsQueuedFollowUpRecoveryJournal()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new CopilotChatStateStore(root);
            Directory.CreateDirectory(store.AttachmentDirectoryPath);
            var attachmentPath = Path.Combine(
                store.AttachmentDirectoryPath,
                "queued-image.png");
            File.WriteAllText(attachmentPath, "queued attachment");
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            var recovery = CreateRecord(
                "run-1",
                conversation.Id,
                "恢复这个提示");
            recovery.ComposerState = CopilotComposerStash.Capture(
                recovery.Prompt,
                recovery.Prompt.Length,
                CopilotAgentMode.Diagnose,
                [CopilotAttachmentItem.CreateImage(attachmentPath)]);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
                QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
                {
                    recovery,
                },
            };

            store.Save(state);
            var loaded = store.Load();
            var deletedCount = store.CleanupOrphanedAttachments(loaded);

            Assert.Equal(CopilotChatStateLoadSource.Primary, store.LastLoadStatus.Source);
            var restoredRecovery = Assert.Single(loaded.QueuedFollowUpRecoveries);
            Assert.Equal("run-1", restoredRecovery.RunId);
            Assert.Equal(conversation.Id, restoredRecovery.ConversationId);
            Assert.Equal("恢复这个提示", restoredRecovery.Prompt);
            var composerState = Assert.IsType<CopilotComposerStash>(
                restoredRecovery.ComposerState);
            Assert.Equal(CopilotAgentMode.Diagnose, composerState.RequestMode);
            Assert.Equal(
                attachmentPath,
                Assert.Single(composerState.Attachments).Value);
            Assert.Equal(0, deletedCount);
            Assert.True(File.Exists(attachmentPath));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void StateStoreRoundTripsDraftRequestMode()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new CopilotChatStateStore(root);
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            conversation.DraftText = "继续诊断";
            conversation.DraftRequestMode = CopilotAgentMode.Diagnose;
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            };

            store.Save(state);
            var loaded = store.Load();

            var restoredConversation = Assert.Single(loaded.Conversations);
            Assert.Equal("继续诊断", restoredConversation.DraftText);
            Assert.Equal(CopilotAgentMode.Diagnose, restoredConversation.DraftRequestMode);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void StateStoreRejectsNonStringRecoveryFields()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new CopilotChatStateStore(root);
            Directory.CreateDirectory(store.StateDirectoryPath);
            var document = new JObject
            {
                [nameof(CopilotChatState.SchemaVersion)] = CopilotChatState.CurrentSchemaVersion,
                [nameof(CopilotChatState.ActiveConversationId)] = "conversation",
                [nameof(CopilotChatState.ActiveProfileId)] = "profile",
                [nameof(CopilotChatState.Conversations)] = new JArray
                {
                    new JObject
                    {
                        [nameof(CopilotConversationRecord.Messages)] = new JArray(),
                        [nameof(CopilotConversationRecord.Attachments)] = new JArray(),
                    },
                },
                [nameof(CopilotChatState.QueuedFollowUpRecoveries)] = new JArray
                {
                    new JObject
                    {
                        [nameof(CopilotQueuedFollowUpRecoveryRecord.RunId)] = "run-1",
                        [nameof(CopilotQueuedFollowUpRecoveryRecord.ConversationId)] = "conversation",
                        [nameof(CopilotQueuedFollowUpRecoveryRecord.Prompt)] = new JArray("invalid"),
                    },
                },
            };
            File.WriteAllText(store.StateFilePath, document.ToString());

            var loaded = store.Load();

            Assert.Equal(CopilotChatStateLoadSource.Unrecoverable, store.LastLoadStatus.Source);
            Assert.Empty(loaded.Conversations);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    private static CopilotQueuedFollowUpRecoveryRecord CreateRecord(string runId, string conversationId, string prompt) => new()
    {
        RunId = runId,
        ConversationId = conversationId,
        Prompt = prompt,
    };

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"copilot-follow-up-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
