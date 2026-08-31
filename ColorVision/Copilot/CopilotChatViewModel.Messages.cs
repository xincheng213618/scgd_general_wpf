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
        private void OpenAttachment(CopilotAttachmentItem? attachment)
        {
            if (attachment == null)
                return;

            try
            {
                switch (attachment.Type)
                {
                    case CopilotAttachmentType.File:
                    case CopilotAttachmentType.Image:
                        OpenFileAttachment(attachment);
                        break;
                    case CopilotAttachmentType.WebPage:
                        OpenWebAttachment(attachment);
                        break;
                    default:
                        ShowTextAttachment(attachment, "查看上下文");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "无法打开附件：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message),
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static void OpenFileAttachment(CopilotAttachmentItem attachment)
        {
            var filePath = CopilotComposerAttachmentService.NormalizeFilePaths([attachment.Value]).FirstOrDefault();
            if (filePath == null || !File.Exists(filePath))
                throw new FileNotFoundException("附件文件不存在或已被移动。", attachment.Value);

            if (CopilotComposerAttachmentService.IsUnsafeFilePath(filePath))
            {
                var revealStartInfo = new ProcessStartInfo("explorer.exe")
                {
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true,
                };
                Process.Start(revealStartInfo);
                return;
            }

            Process.Start(new ProcessStartInfo(filePath)
            {
                UseShellExecute = true,
            });
        }

        private void OpenWebAttachment(CopilotAttachmentItem attachment)
        {
            if (Uri.TryCreate(attachment.Source, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true,
                });
                return;
            }

            ShowTextAttachment(attachment, "查看网页附件");
        }

        private static void ShowTextAttachment(CopilotAttachmentItem attachment, string title)
        {
            var window = new CopilotTextInputWindow(
                title,
                string.IsNullOrWhiteSpace(attachment.DisplayLabel) ? "附件内容" : attachment.DisplayLabel,
                attachment.Value,
                isMultiline: true,
                isReadOnly: true)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.ShowDialog();
        }

        private async Task RemoveAttachment(CopilotAttachmentItem? attachment)
        {
            var conversation = SelectedConversation;
            if (attachment == null || conversation == null
                || Volatile.Read(ref _disposeState) != 0 || IsBusy || HasExclusiveLocalOperation)
                return;

            var messageEditSnapshot = _composerDraftBeforeMessageEdit;
            var attachmentIndex = conversation.Attachments.IndexOf(attachment);
            if (attachmentIndex < 0 || !conversation.Attachments.Remove(attachment))
                return;

            UpdateAttachmentsState(conversation);
            try
            {
                await FlushStatePersistenceBarrierAsync();
            }
            catch
            {
                // A cancelled or reopened edit no longer owns these historical
                // attachments; restoring them would modify the replacement draft.
                var editStillOwnsRemoval = messageEditSnapshot == null
                    || (ReferenceEquals(messageEditSnapshot, _composerDraftBeforeMessageEdit)
                        && string.Equals(_editingConversationId, conversation.Id, StringComparison.Ordinal));
                var contextWasReplaced = messageEditSnapshot == null
                    && !string.Equals(_editingConversationId, conversation.Id, StringComparison.Ordinal)
                    && attachment.Type == CopilotAttachmentType.Context
                    && FindExternalContextAttachment(conversation, attachment.Title, attachment.Source) != null;

                // A concurrent conversation deletion may only have detached this
                // object while its own save is pending. Restore the captured owner
                // so that a failed deletion can put the complete draft back.
                if (editStillOwnsRemoval && !contextWasReplaced && !conversation.Attachments.Contains(attachment))
                {
                    conversation.Attachments.Insert(Math.Min(attachmentIndex, conversation.Attachments.Count), attachment);
                    if (Conversations.Contains(conversation))
                        UpdateAttachmentsState(conversation);
                }
                throw;
            }

            TryDeleteManagedAttachmentFile(attachment);
        }

        private static bool EnsureAssistantHeaders(CopilotConversationRecord conversation, CopilotProfileConfig? profile)
        {
            var assistantHeader = ResolveAssistantHeader(conversation, profile);
            var changed = false;

            foreach (var message in conversation.Messages)
            {
                if (message.IsUser || !string.IsNullOrWhiteSpace(message.AssistantName))
                    continue;

                message.AssistantName = assistantHeader;
                changed = true;
            }

            return changed;
        }

        private static string ResolveAssistantHeader(CopilotProfileConfig profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Model))
                return profile.Model;

            if (!string.IsNullOrWhiteSpace(profile.DisplayLabel))
                return profile.DisplayLabel;

            return "AI";
        }

        private static CopilotChatMessage CreatePendingAssistantMessage(CopilotProfileConfig profile, CopilotAgentMode requestMode)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
            {
                AssistantName = ResolveAssistantHeader(profile),
                RequestMode = requestMode,
            };
            assistantMessage.MarkThinkingStarted();
            return assistantMessage;
        }

        private static string ResolveAssistantHeader(CopilotConversationRecord conversation, CopilotProfileConfig? profile)
        {
            if (profile != null)
                return ResolveAssistantHeader(profile);

            if (!string.IsNullOrWhiteSpace(conversation.ProfileDisplayName))
                return conversation.ProfileDisplayName;

            if (!string.IsNullOrWhiteSpace(conversation.ProfileId))
                return conversation.ProfileId;

            return "AI";
        }

    }
}
