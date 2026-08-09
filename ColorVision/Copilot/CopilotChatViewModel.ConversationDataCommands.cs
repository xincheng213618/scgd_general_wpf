#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.Copilot.Mcp;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Desktop.Feedback;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private async Task ExportConversationFromCommandAsync(CopilotLocalCommand command, string requestedFileName)
        {
            var conversation = SelectedConversation;
            if (!CanExportConversation(conversation))
            {
                ShowLocalCommandResult(
                    command,
                    _isExportingConversation
                        ? "已有会话导出正在执行，请完成后再试。"
                        : "当前会话还没有可导出的已完成消息。");
                return;
            }

            if (!string.IsNullOrWhiteSpace(requestedFileName))
            {
                if (!CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(
                    requestedFileName,
                    out var fileName,
                    out var errorMessage))
                {
                    ShowLocalCommandResult(command, errorMessage);
                    return;
                }

                await ExportConversationAsync(conversation, fileName);
                return;
            }

            var snapshot = CopilotConversationMarkdownExporter.Capture(conversation!);
            var cancellation = BeginAuxiliaryOperation();
            _isExportingConversation = true;
            ShowLocalCommandResult(command, "正在生成当前会话的可见 Markdown 快照。");
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var markdown = await Task.Run(
                    () => CopilotConversationMarkdownExporter.BuildMarkdown(snapshot, cancellation.Token),
                    cancellation.Token);
                if (Volatile.Read(ref _disposeState) == 1)
                    return;
                if (!TrySetClipboardText(markdown, out var errorMessage))
                {
                    ShowLocalCommandResult(command, "复制失败：" + errorMessage);
                    return;
                }

                ShowLocalCommandResult(
                    command,
                    $"已复制当前会话的可见 Markdown（{snapshot.Messages.Count:N0} 条消息，{markdown.Length:N0} 个字符）。");
            }
            finally
            {
                _isExportingConversation = false;
                CompleteAuxiliaryOperation(cancellation);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task OpenFeedbackAsync(string report)
        {
            DismissLocalCommandResult();
            var draft = CopilotFeedbackDraftBuilder.Create(SelectedConversation, report);
            string? temporaryConversationPath = null;
            try
            {
                if (draft.HasConversationAttachment)
                {
                    temporaryConversationPath = Path.Combine(
                        Path.GetTempPath(),
                        $"ColorVision_Copilot_Conversation_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.md");
                    await File.WriteAllTextAsync(
                        temporaryConversationPath,
                        draft.ConversationMarkdown,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }

                if (Volatile.Read(ref _disposeState) == 1)
                    return;

                var attachmentPaths = temporaryConversationPath == null
                    ? Array.Empty<string>()
                    : new[] { temporaryConversationPath };
                var window = new FeedbackWindow(draft.Report, attachmentPaths)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                window.ShowDialog();
            }
            finally
            {
                try
                {
                    if (temporaryConversationPath != null && File.Exists(temporaryConversationPath))
                        File.Delete(temporaryConversationPath);
                }
                catch
                {
                }
            }
        }

        private async Task ExportConversationAsync(
            CopilotConversationRecord? conversation,
            string? suggestedFileName = null)
        {
            if (!CanExportConversation(conversation))
                return;

            var dialog = new SaveFileDialog
            {
                AddExtension = true,
                CheckPathExists = true,
                DefaultExt = ".md",
                FileName = suggestedFileName ?? CopilotConversationMarkdownExporter.BuildFileName(conversation!),
                Filter = "Markdown 文档|*.md|文本文件|*.txt|所有文件|*.*",
                OverwritePrompt = true,
                Title = "导出 Copilot 会话",
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            var snapshot = CopilotConversationMarkdownExporter.Capture(conversation!);
            var cancellation = BeginAuxiliaryOperation();
            _isExportingConversation = true;
            LocalCommandResultTitle = "正在导出会话";
            LocalCommandResultText = dialog.FileName;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var markdown = await Task.Run(
                    () => CopilotConversationMarkdownExporter.BuildMarkdown(snapshot, cancellation.Token),
                    cancellation.Token);
                await WriteConversationExportAsync(dialog.FileName, markdown, cancellation.Token);
                if (Volatile.Read(ref _disposeState) == 1)
                    return;

                LocalCommandResultTitle = "会话已导出";
                LocalCommandResultText = dialog.FileName;
            }
            finally
            {
                _isExportingConversation = false;
                CompleteAuxiliaryOperation(cancellation);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private static async Task WriteConversationExportAsync(string filePath, string content, CancellationToken cancellationToken)
        {
            var destinationPath = Path.GetFullPath(filePath);
            var directoryPath = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException("导出目录不存在或已不可用。");

            var temporaryPath = Path.Combine(directoryPath, $".copilot-export-{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    content,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }

        private async Task DeleteCurrentConversationAsync(CopilotLocalCommand command)
        {
            var target = SelectedConversation;
            if (!CanDeleteConversation(target))
            {
                ShowLocalCommandResult(
                    command,
                    "当前状态不能永久删除会话；请先结束运行、导出或其他独占操作。");
                return;
            }

            var result = await TryDeleteConversationAsync(target);
            if (result.Deleted)
            {
                ShowLocalCommandResult(
                    command,
                    $"已永久删除“{result.DeletedTitle}”。本地消息、草稿和托管附件已移除，不能通过 /unarchive 恢复。"
                    + FormatSessionEndHookDiagnostics(result.HookDiagnostics));
            }
        }

        private async Task DeleteConversationAsync(
            CopilotConversationRecord? conversation)
        {
            await TryDeleteConversationAsync(conversation);
        }

        private async Task<CopilotConversationDeletionResult> TryDeleteConversationAsync(
            CopilotConversationRecord? conversation)
        {
            if (!CanDeleteConversation(conversation))
                return CopilotConversationDeletionResult.NotDeleted;

            var target = conversation!;
            var activeBackgroundCommands =
                CopilotBackgroundShellCommandRegistry.Shared.GetSnapshots(target.Id)
                    .Count(snapshot => snapshot.IsActive);
            if (activeBackgroundCommands > 0)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"无法永久删除“{target.Title}”：当前会话还有 {activeBackgroundCommands:N0} 条后台命令在运行。"
                    + $"{Environment.NewLine}{Environment.NewLine}请先切换到该会话，使用 /ps 查看并停止后台命令；进程树未改变。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return CopilotConversationDeletionResult.NotDeleted;
            }
            var retentionBlocker = GetConversationRetentionBlocker(target);
            if (retentionBlocker != CopilotConversationRetentionBlocker.None)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"无法永久删除“{target.Title}”：{CopilotConversationRetentionPolicy.Describe(retentionBlocker)}。"
                    + $"{Environment.NewLine}{Environment.NewLine}请先处理或明确放弃该状态；若只想隐藏安全会话，请使用 /archive。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return CopilotConversationDeletionResult.NotDeleted;
            }

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"永久删除“{target.Title}”？"
                + $"{Environment.NewLine}{Environment.NewLine}本地消息、草稿和托管附件会被移除，且不能通过 /unarchive 恢复。"
                + $"{Environment.NewLine}若只想隐藏，请选择“否”并使用 /archive。",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return CopilotConversationDeletionResult.NotDeleted;
            }

            _isEndingConversation = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var hookDiagnostics = await EndConversationSessionAsync(target);
                if (!Conversations.Contains(target))
                    return CopilotConversationDeletionResult.NotDeleted;

                var deletedTitle = target.Title;
                var wasSelected = ReferenceEquals(target, SelectedConversation);
                _conversationTitleCoordinator.Cancel(target.Id);
                var managedAttachments = target.EnumerateReferencedAttachments().ToArray();
                ClearAgentRunNoticeForConversation(target.Id);
                AcknowledgeCompletionNotices(target.Id);

                var currentIndex = Conversations.IndexOf(target);
                if (!Conversations.Remove(target))
                {
                    _turnRuntime.QueueSessionStart(
                        target.Id,
                        CopilotCodexSessionStartSource.Resume);
                    return CopilotConversationDeletionResult.NotDeleted;
                }

                RemoveQueuedFollowUpRecoveryRecords(target.Id);
                CopilotBackgroundShellCommandRegistry.Shared.ClearCompleted(target.Id);
                CopilotShellCommandOutputArchiveRegistry.Shared.ClearConversation(
                    target.Id);
                RemoveManagedAttachmentFiles(managedAttachments);

                if (wasSelected)
                {
                    var replacement = CopilotConversationRetentionPolicy.FindNearestActive(
                        Conversations,
                        currentIndex)
                        ?? CreateConversation();
                    SelectConversation(replacement, persist: false);
                }

                PersistState(immediate: true);
                return new CopilotConversationDeletionResult(
                    true,
                    deletedTitle,
                    hookDiagnostics);
            }
            finally
            {
                _isEndingConversation = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private sealed record CopilotConversationDeletionResult(
            bool Deleted,
            string DeletedTitle,
            IReadOnlyList<string> HookDiagnostics)
        {
            public static CopilotConversationDeletionResult NotDeleted { get; } =
                new(false, string.Empty, Array.Empty<string>());
        }

        private bool CanDeleteConversation(CopilotConversationRecord? conversation) =>
            Volatile.Read(ref _disposeState) == 0
            && conversation != null
            && Conversations.Contains(conversation)
            && !IsBusy
            && !HasExclusiveLocalOperation
            && !_isExportingConversation;

        private void RemoveQueuedFollowUpRecoveryRecords(string conversationId)
        {
            if (_state.QueuedFollowUpRecoveries == null)
                return;

            for (var index = _state.QueuedFollowUpRecoveries.Count - 1; index >= 0; index--)
            {
                if (string.Equals(
                    _state.QueuedFollowUpRecoveries[index]?.ConversationId,
                    conversationId,
                    StringComparison.Ordinal))
                {
                    _state.QueuedFollowUpRecoveries.RemoveAt(index);
                }
            }
        }

        private void TogglePinConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            conversation.IsPinned = !conversation.IsPinned;
            CopilotConversationService.MoveToPreferredIndex(Conversations, conversation);
            PersistState();
        }

    }
}
