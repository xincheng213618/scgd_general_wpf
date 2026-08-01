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
        private void ToggleConversationSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            _isConversationSidebarExpanded = !_isConversationSidebarExpanded;
            UpdateResponsiveLayout();
        }

        private void ConversationFindTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not CopilotChatViewModel viewModel)
                return;

            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (viewModel.CloseConversationFindCommand.CanExecute(null))
                    viewModel.CloseConversationFindCommand.Execute(null);
                FocusPromptInput();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter
                || Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift))
            {
                return;
            }

            var previous = Keyboard.Modifiers == ModifierKeys.Shift;
            if (viewModel.MoveConversationFind(previous))
                e.Handled = true;
        }

        private void ConversationSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _hasConversationSearchPreviewSelection = false;
        }

        private void ConversationSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (RenameConversationSearchSelection())
                    e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            var handled = e.Key switch
            {
                Key.Up => MoveConversationSearchSelection(-1),
                Key.Down => MoveConversationSearchSelection(1),
                Key.Enter => CommitConversationSearchSelection(focusPrompt: true),
                _ => false,
            };
            if (handled)
                e.Handled = true;
        }

        private void ConversationListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _hasConversationSearchPreviewSelection = ConversationListBox.SelectedIndex >= 0;
                if (RenameConversationSearchSelection())
                    e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
                return;

            _hasConversationSearchPreviewSelection = ConversationListBox.SelectedIndex >= 0;
            if (CommitConversationSearchSelection(focusPrompt: true))
                e.Handled = true;
        }

        private void ConversationListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source
                || ItemsControl.ContainerFromElement(ConversationListBox, source) is not ListBoxItem item
                || item.DataContext is not CopilotConversationRecord conversation)
            {
                return;
            }

            var clickedIndex = ConversationListBox.Items.IndexOf(conversation);
            if (clickedIndex < 0)
                return;

            ConversationListBox.SelectedIndex = clickedIndex;
            _hasConversationSearchPreviewSelection = true;
            if (CommitConversationSearchSelection(focusPrompt: false))
                e.Handled = true;
        }

        private bool MoveConversationSearchSelection(int direction)
        {
            FlushConversationSearchResults();
            var targetIndex = CopilotConversationService.ResolveSearchNavigationIndex(
                ConversationListBox.Items.Count,
                ConversationListBox.SelectedIndex,
                _hasConversationSearchPreviewSelection,
                direction);
            if (targetIndex < 0)
                return false;

            _hasConversationSearchPreviewSelection = true;
            ConversationListBox.SelectedIndex = targetIndex;
            ConversationListBox.ScrollIntoView(ConversationListBox.Items[targetIndex]);
            return true;
        }

        private bool CommitConversationSearchSelection(bool focusPrompt)
        {
            FlushConversationSearchResults();
            var targetIndex = CopilotConversationService.ResolveSearchCommitIndex(
                ConversationListBox.Items.Count,
                ConversationListBox.SelectedIndex,
                _hasConversationSearchPreviewSelection);
            if (targetIndex < 0
                || ConversationListBox.Items[targetIndex] is not CopilotConversationRecord conversation
                || DataContext is not CopilotChatViewModel viewModel
                || !viewModel.SelectConversationCommand.CanExecute(conversation))
            {
                return false;
            }

            ConversationListBox.SelectedIndex = targetIndex;
            viewModel.SelectConversationCommand.Execute(conversation);
            _hasConversationSearchPreviewSelection = false;
            if (focusPrompt)
                FocusPromptInput();
            return true;
        }

        private bool RenameConversationSearchSelection()
        {
            FlushConversationSearchResults();
            var targetIndex = CopilotConversationService.ResolveSearchCommitIndex(
                ConversationListBox.Items.Count,
                ConversationListBox.SelectedIndex,
                _hasConversationSearchPreviewSelection);
            if (targetIndex < 0
                || ConversationListBox.Items[targetIndex] is not CopilotConversationRecord conversation
                || DataContext is not CopilotChatViewModel viewModel
                || !viewModel.RenameConversationCommand.CanExecute(conversation))
            {
                return false;
            }

            ConversationListBox.SelectedIndex = targetIndex;
            _hasConversationSearchPreviewSelection = true;
            viewModel.RenameConversationCommand.Execute(conversation);
            var renamedIndex = ConversationListBox.Items.IndexOf(conversation);
            _hasConversationSearchPreviewSelection = renamedIndex >= 0;
            ConversationListBox.SelectedIndex = renamedIndex;
            if (renamedIndex >= 0)
            {
                ConversationListBox.ScrollIntoView(conversation);
            }
            return true;
        }

        private void FlushConversationSearchResults()
        {
            if (DataContext is not CopilotChatViewModel viewModel)
                return;

            var selectionAnchor = _hasConversationSearchPreviewSelection
                ? ConversationListBox.SelectedItem as CopilotConversationRecord
                : null;
            if (!viewModel.FlushConversationSearchRefresh())
                return;

            var anchoredIndex = selectionAnchor == null
                ? -1
                : ConversationListBox.Items.IndexOf(selectionAnchor);
            _hasConversationSearchPreviewSelection = anchoredIndex >= 0;
            ConversationListBox.SelectedIndex = anchoredIndex;
        }

    }
}
