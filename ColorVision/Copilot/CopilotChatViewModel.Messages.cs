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

        private void RemoveAttachment(CopilotAttachmentItem? attachment)
        {
            if (attachment == null || SelectedConversation == null)
                return;

            if (!SelectedConversation.Attachments.Remove(attachment))
                return;

            if (!SelectedConversation.Messages
                .SelectMany(message => message.Attachments)
                .Any(candidate => string.Equals(candidate.Value, attachment.Value, StringComparison.OrdinalIgnoreCase)))
            {
                TryDeleteManagedAttachmentFile(attachment);
            }

            UpdateAttachmentsState(SelectedConversation);
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
