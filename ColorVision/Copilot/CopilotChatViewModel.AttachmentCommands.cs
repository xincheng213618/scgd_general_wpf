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
        private async Task AddFileAttachmentAsync()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true,
                Filter = "All files|*.*",
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            await AddFileAttachmentsAsync(dialog.FileNames);
        }

        private void AttachActiveDocument()
        {
            var activeDocumentPath = _activeDocumentPath;
            if (!CanAttachActiveDocument)
                return;
            if (AddFileAttachments([activeDocumentPath]) > 0 || File.Exists(activeDocumentPath))
                return;

            LocalCommandResultTitle = "无法附加当前文件";
            LocalCommandResultText = "当前文件已关闭、已移动或不再可读取。";
            _activeDocumentPath = TryGetActiveDocumentPath();
            OnActiveDocumentStateChanged();
        }

        public int AddFileAttachments(IEnumerable<string>? filePaths)
        {
            if (IsBusy || filePaths == null)
                return 0;

            var normalizedPaths = CopilotComposerAttachmentService.NormalizeFilePaths(filePaths);
            return AddResolvedFileAttachments(CopilotComposerAttachmentService.FilterExistingFilePaths(normalizedPaths, CancellationToken.None));
        }

        internal async Task<int> AddFileAttachmentsAsync(IEnumerable<string>? filePaths)
        {
            if (IsBusy || filePaths == null)
                return 0;

            var normalizedPaths = CopilotComposerAttachmentService.NormalizeFilePaths(filePaths);
            if (normalizedPaths.Length == 0)
                return 0;

            var conversation = EnsureConversation();
            var cancellation = BeginAuxiliaryOperation();
            _fileAttachmentCts = cancellation;
            IsBusy = true;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var resolveTask = Task.Run(
                    () => CopilotComposerAttachmentService.FilterExistingFilePaths(normalizedPaths, cancellation.Token),
                    CancellationToken.None);
                var existingPaths = await resolveTask.WaitAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (Volatile.Read(ref _disposeState) == 1 || !Conversations.Contains(conversation))
                    return 0;

                return AddResolvedFileAttachments(existingPaths, conversation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return 0;
            }
            catch (Exception ex)
            {
                LocalCommandResultTitle = "附加文件 · 失败";
                LocalCommandResultText = CopilotUserFacingErrorFormatter.Sanitize(ex.Message);
                return 0;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (ReferenceEquals(_fileAttachmentCts, cancellation))
                    _fileAttachmentCts = null;
                CompleteAuxiliaryOperation(cancellation);
                IsBusy = _taskHost.IsActive;
            }
        }

        private int AddResolvedFileAttachments(
            IReadOnlyList<string> filePaths,
            CopilotConversationRecord? conversation = null)
        {
            if (filePaths.Count == 0)
                return 0;

            conversation ??= EnsureConversation();
            var addedCount = 0;
            var attachmentLimitReached = false;
            var imageLimitReached = false;
            foreach (var filePath in filePaths)
            {
                if (conversation.Attachments.Any(item => (item.Type is CopilotAttachmentType.File or CopilotAttachmentType.Image)
                    && string.Equals(item.Value, filePath, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (conversation.Attachments.Count >= CopilotComposerAttachmentService.MaximumAttachmentCount)
                {
                    attachmentLimitReached = true;
                    break;
                }

                var isImage = CopilotImagePayloadLoader.IsSupportedImageFileName(filePath);
                if (isImage
                    && conversation.Attachments.Count(item => item.Type == CopilotAttachmentType.Image) >= CopilotImagePayloadLoader.MaximumImages)
                {
                    imageLimitReached = true;
                    continue;
                }

                conversation.Attachments.Add(isImage ? CopilotAttachmentItem.CreateImage(filePath) : CopilotAttachmentItem.CreateFile(filePath));
                addedCount++;
            }

            if (addedCount > 0)
                UpdateAttachmentsState(conversation);
            ReportFileAttachmentLimits(conversation, addedCount, attachmentLimitReached, imageLimitReached);
            return addedCount;
        }

        private void AddContextAttachment()
        {
            var conversation = EnsureConversation();
            if (!TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.Context))
                return;

            var window = new CopilotTextInputWindow(
                "Attach Context",
                "Enter the context to attach to this chat",
                string.Empty,
                isMultiline: true,
                maximumLength: CopilotAttachmentItem.MaximumStoredTextCharacters)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            conversation.Attachments.Add(CopilotAttachmentItem.CreateContext(window.ResultText));
            UpdateAttachmentsState(conversation);
        }

        private void AttachCurrentLiveContext()
        {
            var liveContext = _currentLiveContext;
            if (liveContext == null || liveContext.SnapshotItems == null || liveContext.SnapshotItems.Count == 0)
                return;

            var conversation = EnsureConversation();
            _ = AttachExternalContextSnapshot(
                conversation,
                string.IsNullOrWhiteSpace(liveContext.AttachmentTitle) ? liveContext.Title : liveContext.AttachmentTitle,
                liveContext.SourceId,
                liveContext.SnapshotItems);
        }

        private async Task AddWebPageAttachmentAsync()
        {
            var conversation = EnsureConversation();
            var window = new CopilotTextInputWindow(
                "Attach Web Page",
                "Enter the web page URL to fetch and attach",
                "https://",
                maximumLength: CopilotWebPageToolSupport.MaxWebPageUrlCharacters)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            var url = NormalizeWebPageUrl(window.ResultText);
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "The web page URL is invalid.",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var existingAttachment = conversation.Attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.WebPage && string.Equals(item.Source, url, StringComparison.OrdinalIgnoreCase));
            if (existingAttachment == null && !TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.WebPage))
                return;

            var cancellation = BeginAuxiliaryOperation();
            _webPageAttachmentCts = cancellation;
            IsBusy = true;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var webPage = await LoadWebPageContentAsync(url, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (Volatile.Read(ref _disposeState) == 1 || !Conversations.Contains(conversation))
                    return;

                var attachment = CopilotAttachmentItem.CreateWebPage(url, webPage.Title, BuildStoredWebPageContent(webPage));

                if (existingAttachment != null)
                {
                    existingAttachment.Title = attachment.Title;
                    existingAttachment.Value = attachment.Value;
                    existingAttachment.Source = attachment.Source;
                    existingAttachment.CreatedAt = attachment.CreatedAt;
                }
                else
                {
                    if (!TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.WebPage))
                        return;

                    conversation.Attachments.Add(attachment);
                }

                UpdateAttachmentsState(conversation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "Failed to fetch web page: the request timed out.",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to fetch web page: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (ReferenceEquals(_webPageAttachmentCts, cancellation))
                    _webPageAttachmentCts = null;
                CompleteAuxiliaryOperation(cancellation);
                IsBusy = _taskHost.IsActive;
            }
        }

        private void PasteImageAttachment()
        {
            if (TryBeginPasteClipboardImageAttachment(out var operation))
            {
                RunUiOperation(async () => await operation, "粘贴图片");
                return;
            }

            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                "The clipboard does not contain an image that can be attached.",
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        internal bool TryBeginPasteClipboardImageAttachment(out Task<bool> operation)
        {
            operation = Task.FromResult(false);
            if (IsBusy)
                return false;

            try
            {
                if (!TryGetFrozenClipboardImage(out var image))
                    return false;

                operation = SaveClipboardImageAttachmentAsync(image);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to paste image: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }
        }

        public bool TryPasteClipboardImageAttachment()
        {
            if (IsBusy)
                return false;

            try
            {
                if (!TryGetFrozenClipboardImage(out var image))
                    return false;

                var conversation = EnsureConversation();
                if (!TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.Image))
                    return false;
                var imagePath = SaveClipboardImage(image, CancellationToken.None);
                return AddClipboardImageAttachment(conversation, imagePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to paste image: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        private async Task<bool> SaveClipboardImageAttachmentAsync(BitmapSource image)
        {
            var conversation = EnsureConversation();
            if (!TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.Image))
                return false;

            var cancellation = BeginAuxiliaryOperation();
            Task<string>? saveTask = null;
            _fileAttachmentCts = cancellation;
            IsBusy = true;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                saveTask = Task.Run(
                    () => SaveClipboardImage(image, cancellation.Token),
                    CancellationToken.None);
                var imagePath = await saveTask.WaitAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (Volatile.Read(ref _disposeState) == 1 || !Conversations.Contains(conversation))
                {
                    CopilotChatStateStore.TryDeleteManagedAttachmentFile(_stateStore.AttachmentDirectoryPath, imagePath);
                    return false;
                }

                return AddClipboardImageAttachment(conversation, imagePath);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                if (saveTask != null)
                    CleanupCancelledClipboardImage(saveTask);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to paste image: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (ReferenceEquals(_fileAttachmentCts, cancellation))
                    _fileAttachmentCts = null;
                CompleteAuxiliaryOperation(cancellation);
                IsBusy = _taskHost.IsActive;
            }
        }

        private static bool TryGetFrozenClipboardImage(out BitmapSource image)
        {
            image = null!;
            if (!Clipboard.ContainsImage())
                return false;

            var clipboardImage = Clipboard.GetImage();
            if (clipboardImage == null)
                return false;
            if (!clipboardImage.IsFrozen)
            {
                if (clipboardImage.CanFreeze)
                {
                    clipboardImage.Freeze();
                }
                else
                {
                    var copy = new WriteableBitmap(clipboardImage);
                    copy.Freeze();
                    clipboardImage = copy;
                }
            }

            image = clipboardImage;
            return true;
        }

        private void CleanupCancelledClipboardImage(Task<string> task)
        {
            var attachmentDirectoryPath = _stateStore.AttachmentDirectoryPath;
            _ = task.ContinueWith(
                completed =>
                {
                    if (completed.IsCompletedSuccessfully)
                        CopilotChatStateStore.TryDeleteManagedAttachmentFile(attachmentDirectoryPath, completed.Result);
                    else
                        _ = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private bool AddClipboardImageAttachment(CopilotConversationRecord conversation, string imagePath)
        {
            if (!TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.Image))
            {
                CopilotChatStateStore.TryDeleteManagedAttachmentFile(_stateStore.AttachmentDirectoryPath, imagePath);
                return false;
            }

            var title = $"Pasted Image {DateTime.Now:HH:mm:ss}";
            conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(imagePath, title));
            UpdateAttachmentsState(conversation);
            return true;
        }


    }
}
