using ColorVision.Themes;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatPanel
    {
        private const double MessageBottomThreshold = 36;

        private async void PromptTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (DataContext is not CopilotChatViewModel viewModel)
                return;

            if (!viewModel.TryBeginPasteClipboardImageAttachment(out var operation))
                return;

            e.CancelCommand();
            await operation;
        }

        private void ComposerShellBorder_PreviewDragOver(object sender, DragEventArgs e)
        {
            var canAttach = DataContext is CopilotChatViewModel { IsBusy: false }
                && TryGetDroppedFiles(e.Data, out _);
            FileDropOverlay.Visibility = canAttach ? Visibility.Visible : Visibility.Collapsed;
            e.Effects = canAttach ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void ComposerShellBorder_PreviewDragLeave(object sender, DragEventArgs e)
        {
            FileDropOverlay.Visibility = Visibility.Collapsed;
        }

        private async void ComposerShellBorder_PreviewDrop(object sender, DragEventArgs e)
        {
            FileDropOverlay.Visibility = Visibility.Collapsed;
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (DataContext is not CopilotChatViewModel { IsBusy: false } viewModel
                || !TryGetDroppedFiles(e.Data, out var filePaths))
            {
                return;
            }

            e.Effects = DragDropEffects.Copy;
            if (await viewModel.AddFileAttachmentsAsync(filePaths) == 0)
                return;

            FocusPromptInput();
        }

        private static bool TryGetDroppedFiles(IDataObject data, out string[] filePaths)
        {
            filePaths = Array.Empty<string>();
            if (!data.GetDataPresent(DataFormats.FileDrop)
                || data.GetData(DataFormats.FileDrop) is not string[] droppedPaths)
            {
                return false;
            }

            filePaths = droppedPaths
                .Where(filePath => !string.IsNullOrWhiteSpace(filePath))
                .ToArray();
            return filePaths.Length > 0;
        }

        private bool IsNearBottom()
        {
            var scrollViewer = GetMessagesScrollViewer();
            return scrollViewer == null || scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset <= MessageBottomThreshold;
        }

        private void CancelPendingMessageNavigation()
        {
            _messageNavigationVersion++;
            _messageNavigationOperation?.Abort();
            _messageNavigationOperation = null;
            _isScrollToBottomPending = false;
            _isFindNavigationPending = false;
        }

        private void CompleteMessageNavigation(long version)
        {
            if (version != _messageNavigationVersion)
                return;

            _messageNavigationOperation = null;
            _isScrollToBottomPending = false;
            _isFindNavigationPending = false;
            if (IsNearBottom())
                HideScrollToLatestButton();
            else
                ShowScrollToLatestButton();
        }

        private void ScrollToBottom(bool isExplicitNavigation = false)
        {
            var viewModel = _attachedViewModel;
            if (viewModel == null || !isExplicitNavigation && _messageNavigationOperation != null)
                return;

            CancelPendingMessageNavigation();
            var messages = viewModel.Messages;
            var version = _messageNavigationVersion;
            _isScrollToBottomPending = true;
            HideScrollToLatestButton();
            _messageNavigationOperation = Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    if (version != _messageNavigationVersion
                        || !ReferenceEquals(viewModel, _attachedViewModel)
                        || !ReferenceEquals(messages, viewModel.Messages))
                        return;

                    // ScrollIntoView would anchor a tall last row at its top and can override the end offset.
                    GetMessagesScrollViewer()?.ScrollToEnd();
                }
                finally
                {
                    CompleteMessageNavigation(version);
                }
            });
        }

        private void ShowScrollToLatestButton()
        {
            if (!_isScrollToBottomPending && !IsNearBottom())
                ScrollToLatestButton.Visibility = Visibility.Visible;
        }

        private void HideScrollToLatestButton()
        {
            ScrollToLatestButton.Visibility = Visibility.Collapsed;
        }

        private void MessagesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = GetMessagesScrollViewer();
            if (scrollViewer == null || !ReferenceEquals(e.OriginalSource, scrollViewer))
                return;

            if (_isScrollToBottomPending
                && _messageNavigationOperation?.Status == DispatcherOperationStatus.Pending
                && e.VerticalChange < 0
                && e.ExtentHeightChange >= 0
                && e.ViewportHeightChange == 0
                && !IsNearBottom())
            {
                CancelPendingMessageNavigation();
            }

            // Markdown can finish layout after its Content-triggered follow has run.
            // Use the previous viewport, and never treat an upward scroll as new content.
            var previousViewportHeight = e.ViewportHeight - e.ViewportHeightChange;
            var previousScrollableHeight = Math.Max(0, e.ExtentHeight - e.ExtentHeightChange - previousViewportHeight);
            var previousOffset = e.VerticalOffset - e.VerticalChange;
            if (e.ExtentHeightChange > 0
                && e.VerticalChange >= 0
                && previousViewportHeight > 0
                && e.ViewportHeight > 0
                && previousScrollableHeight - previousOffset <= MessageBottomThreshold)
            {
                ScrollToBottom();
            }

            if (IsNearBottom())
                HideScrollToLatestButton();
            else
                ShowScrollToLatestButton();
        }

        private ScrollViewer? GetMessagesScrollViewer()
        {
            _messagesScrollViewer ??= FindVisualChild<ScrollViewer>(MessagesListBox);
            return _messagesScrollViewer;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                    return match;

                var nestedMatch = FindVisualChild<T>(child);
                if (nestedMatch != null)
                    return nestedMatch;
            }

            return null;
        }

    }
}
