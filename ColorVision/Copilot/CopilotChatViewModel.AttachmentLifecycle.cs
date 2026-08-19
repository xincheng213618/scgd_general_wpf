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
        private void UpdateAttachmentsState(CopilotConversationRecord conversation)
        {
            conversation.RefreshSummary();
            RefreshFilteredConversations();
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            InvalidateChatAttachmentTokenEstimate();
            RefreshComposerTokenEstimate();
            PersistState();
            OnCurrentLiveContextStateChanged();
            OnActiveDocumentStateChanged();
        }

        private void ConsumeCapturedComposerAttachments(
            CopilotConversationRecord conversation,
            IReadOnlyList<CopilotAttachmentItem> capturedAttachments)
        {
            if (capturedAttachments.Count == 0 || conversation.Attachments.Count == 0)
                return;

            if (CopilotComposerAttachmentService.RemoveCapturedByReference(
                    conversation.Attachments,
                    capturedAttachments) > 0)
            {
                UpdateAttachmentsState(conversation);
            }
        }

        private bool AttachExternalContextSnapshot(
            CopilotConversationRecord conversation,
            string? attachmentTitle,
            string? attachmentSourceId,
            IReadOnlyList<CopilotContextItem> contextItems)
        {
            var content = CopilotConversationRequestBuilder.BuildContextAttachmentContent(contextItems);
            if (string.IsNullOrWhiteSpace(content))
                return true;

            var normalizedTitle = string.IsNullOrWhiteSpace(attachmentTitle)
                ? contextItems.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Title))?.Title ?? "Attached Context"
                : attachmentTitle.Trim();

            CopilotAttachmentItem? existingAttachment;
            if (!string.IsNullOrWhiteSpace(attachmentSourceId))
            {
                existingAttachment = conversation.Attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.Context
                    && string.Equals(item.Source, attachmentSourceId, StringComparison.Ordinal));
            }
            else
            {
                existingAttachment = conversation.Attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.Context
                    && string.Equals(item.Title, normalizedTitle, StringComparison.Ordinal));
            }

            if (existingAttachment != null)
            {
                var attachment = CopilotAttachmentItem.CreateContext(content, normalizedTitle, attachmentSourceId);
                existingAttachment.Title = attachment.Title;
                existingAttachment.Value = attachment.Value;
                existingAttachment.Source = attachment.Source;
                existingAttachment.CreatedAt = attachment.CreatedAt;
            }
            else
            {
                if (!TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.Context))
                    return false;

                conversation.Attachments.Add(CopilotAttachmentItem.CreateContext(content, normalizedTitle, attachmentSourceId));
            }

            UpdateAttachmentsState(conversation);
            return true;
        }

        private static string BuildStoredWebPageContent(CopilotFetchedWebPageContent page) =>
            CopilotWebPageToolSupport.BuildStoredWebPageContent(page);

        private string SaveClipboardImage(BitmapSource image, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_stateStore.AttachmentDirectoryPath);

            var filePath = Path.Combine(
                _stateStore.AttachmentDirectoryPath,
                $"clipboard-{DateTime.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    encoder.Save(stream);
                cancellationToken.ThrowIfCancellationRequested();

                if (new FileInfo(filePath).Length > CopilotImagePayloadLoader.MaximumImageBytes)
                {
                    throw new InvalidOperationException(
                        $"粘贴的图片超过 {CopilotImagePayloadLoader.MaximumImageBytes / 1024 / 1024} MB 限制，请先缩小图片后重试。");
                }

                return filePath;
            }
            catch
            {
                CopilotChatStateStore.TryDeleteManagedAttachmentFile(_stateStore.AttachmentDirectoryPath, filePath);
                throw;
            }
        }

        private async Task<IReadOnlyList<CopilotAttachmentItem>?> TryPersistImageAttachmentsAsync(
            IReadOnlyList<CopilotAttachmentItem> attachments)
        {
            try
            {
                return await CopilotImageAttachmentAdmission.PersistAsync(
                    attachments,
                    _stateStore.AttachmentDirectoryPath,
                    CancellationToken.None);
            }
            catch (CopilotImageAttachmentAdmissionException ex)
            {
                LocalCommandResultTitle = ex.FailureKind
                    == CopilotImageAttachmentAdmissionFailureKind.RejectedInput
                        ? "图片无法附加"
                        : "图片保存失败";
                LocalCommandResultText = ex.Message;
                return null;
            }
        }

        private void RemoveManagedAttachmentFiles(IEnumerable<CopilotAttachmentItem> attachments)
        {
            foreach (var attachment in attachments.ToList())
            {
                TryDeleteManagedAttachmentFile(attachment);
            }
        }

        private void TryDeleteManagedAttachmentFile(CopilotAttachmentItem attachment)
        {
            if (!attachment.IsStoredImageFile || string.IsNullOrWhiteSpace(attachment.Value))
                return;

            if (Conversations
                .SelectMany(conversation => conversation.EnumerateReferencedAttachments())
                .Concat(_followUpQueue.EnumerateReferencedAttachments())
                .Any(candidate => candidate.IsStoredImageFile
                    && string.Equals(candidate.Value, attachment.Value, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            CopilotChatStateStore.TryDeleteManagedAttachmentFile(_stateStore.AttachmentDirectoryPath, attachment.Value);
        }

        private static string NormalizeWebPageUrl(string value) => CopilotWebPageToolSupport.NormalizeWebPageUrl(value);

        private static Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(string url, CancellationToken cancellationToken) =>
            CopilotWebPageToolSupport.LoadWebPageContentAsync(url, cancellationToken);
    }
}
