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
            const double threshold = 36;
            var scrollViewer = GetMessagesScrollViewer();
            return scrollViewer == null || scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset <= threshold;
        }

        private void ScrollToBottom()
        {
            if (_isScrollToBottomPending)
                return;

            _isScrollToBottomPending = true;
            HideScrollToLatestButton();
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    if (MessagesListBox.Items.Count > 0)
                        MessagesListBox.ScrollIntoView(MessagesListBox.Items[MessagesListBox.Items.Count - 1]);
                    GetMessagesScrollViewer()?.ScrollToEnd();
                }
                finally
                {
                    _isScrollToBottomPending = false;
                    HideScrollToLatestButton();
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
