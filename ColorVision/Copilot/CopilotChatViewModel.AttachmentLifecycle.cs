#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private readonly Dictionary<CopilotNonBlockingCancellationSource, (string ConversationId, string? HostedRunId)> _imagePreparationCancellations = new();

        private bool HasSelectedImagePreparation => SelectedConversation != null
            && _imagePreparationCancellations.Any(item => !item.Key.IsCancellationRequested
                && string.Equals(item.Value.ConversationId, SelectedConversation.Id, StringComparison.Ordinal));

        private bool TryCancelSelectedImagePreparation()
        {
            var pending = _imagePreparationCancellations
                .Where(item => !item.Key.IsCancellationRequested
                    && string.Equals(item.Value.ConversationId, SelectedConversation?.Id, StringComparison.Ordinal))
                .ToArray();
            foreach (var preparation in pending)
            {
                preparation.Key.RequestCancellation();
                if (preparation.Value.HostedRunId is { } runId)
                    _taskHost.RequestCancel(runId);
            }
            if (pending.Length > 0)
            {
                LocalCommandResultTitle = "请求准备已取消";
                LocalCommandResultText = "尚未发送新请求，输入和附件均已保留。";
                NotifyImagePreparationChanged();
            }
            return pending.Length > 0;
        }

        private void NotifyImagePreparationChanged()
        {
            OnPropertyChanged(nameof(PrimaryActionGlyph));
            OnPropertyChanged(nameof(PrimaryActionToolTip));
        }

        private void UpdateAttachmentsState(CopilotConversationRecord conversation)
        {
            conversation.RefreshSummary();
            RefreshFilteredConversations();
            PersistState();
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

            var existingAttachment = FindExternalContextAttachment(conversation.Attachments, normalizedTitle, attachmentSourceId);

            if (existingAttachment != null)
            {
                var attachment = CopilotAttachmentItem.CreateContext(content, normalizedTitle, attachmentSourceId);
                conversation.Attachments[conversation.Attachments.IndexOf(existingAttachment)] = attachment;
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

        private static CopilotAttachmentItem? FindExternalContextAttachment(
            IEnumerable<CopilotAttachmentItem> attachments,
            string title,
            string? sourceId) => attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.Context
                && (!string.IsNullOrWhiteSpace(sourceId)
                    ? string.Equals(item.Source, sourceId, StringComparison.Ordinal)
                    : string.Equals(item.Title, title, StringComparison.Ordinal)));

        private static string BuildStoredWebPageContent(CopilotFetchedWebPageContent page) =>
            CopilotWebPageToolSupport.BuildStoredWebPageContent(page);

        private string SaveClipboardImage(BitmapSource image, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attachmentRoot = CopilotImageAttachmentAdmission.PrepareStorageDirectory(_stateStore.AttachmentDirectoryPath);

            var filePath = Path.Combine(
                attachmentRoot,
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
            IReadOnlyList<CopilotAttachmentItem> attachments,
            CopilotManagedImageAttachmentLease imageLease,
            CopilotConversationRecord conversation,
            CopilotHostedAgentRun? hostedRun,
            Func<bool>? canApplyFeedback = null)
        {
            if (!attachments.Any(attachment => attachment.Type == CopilotAttachmentType.Image))
                return attachments;

            var feedbackRevision = Volatile.Read(ref _localCommandResultRevision);
            var cancellation = BeginAuxiliaryOperation();
            using var queuedCancellation = hostedRun?.CancellationToken.Register(cancellation.RequestCancellation);
            _imagePreparationCancellations.Add(cancellation, (conversation.Id, hostedRun?.Id));
            NotifyImagePreparationChanged();
            try
            {
                var admitted = await CopilotImageAttachmentAdmission.PersistAsync(
                    attachments,
                    _stateStore.AttachmentDirectoryPath,
                    cancellation.Token,
                    imageLease);
                cancellation.Token.ThrowIfCancellationRequested();
                return admitted;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // The stop action already reported cancellation. A late continuation
                // must not overwrite feedback from a newer request or another conversation.
                return null;
            }
            catch (CopilotImageAttachmentAdmissionException ex)
            {
                if (!cancellation.IsCancellationRequested
                    && (canApplyFeedback?.Invoke() ?? true)
                    && CanApplyAuxiliaryConversationFeedback(conversation, feedbackRevision))
                    ReportImageAttachmentAdmissionFailure(ex);
                return null;
            }
            finally
            {
                _imagePreparationCancellations.Remove(cancellation);
                CompleteAuxiliaryOperation(cancellation);
                NotifyImagePreparationChanged();
            }
        }

        private void ReportImageAttachmentAdmissionFailure(
            CopilotImageAttachmentAdmissionException exception)
        {
            LocalCommandResultTitle = exception.FailureKind
                == CopilotImageAttachmentAdmissionFailureKind.RejectedInput
                    ? "图片无法附加"
                    : "图片保存失败";
            LocalCommandResultText = exception.Message;
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
                .Concat(_composerDraftBeforeMessageEdit?.Attachments ?? Enumerable.Empty<CopilotAttachmentItem>())
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
