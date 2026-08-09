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
        private void VoiceInputButton_Click(object sender, RoutedEventArgs e)
        {
            PromptTextBox.Focus();
            Keyboard.Focus(PromptTextBox);
            CopilotUiTaskObserver.Run(
                ActivateVoiceInputAsync,
                "启动 Windows 语音输入",
                message => MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "无法启动语音输入：" + message,
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
        }

        private static async Task ActivateVoiceInputAsync()
        {
            await Task.Delay(80);
            SendWindowsVoiceTypingShortcut();
        }

        private async void PromptTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CopilotChatViewModel historyScopeViewModel
                && historyScopeViewModel.IsPromptHistorySearchOpen
                && e.Key == Key.S
                && Keyboard.Modifiers == ModifierKeys.Control)
            {
                historyScopeViewModel.TryTogglePromptHistorySearchScope();
                e.Handled = true;
                return;
            }

            if (DataContext is CopilotChatViewModel promptHistoryViewModel
                && promptHistoryViewModel.IsPromptHistorySearchOpen
                && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (e.Key == Key.Escape)
                {
                    promptHistoryViewModel.DismissPromptHistorySearch();
                    MovePromptCaretToEnd();
                    e.Handled = true;
                    return;
                }
                if (e.Key is Key.Up or Key.Down
                    && promptHistoryViewModel.TryNavigatePromptHistorySearch(previous: e.Key == Key.Up))
                {
                    PromptHistorySearchListBox.SelectedItem =
                        promptHistoryViewModel.SelectedPromptHistorySearchResult;
                    if (PromptHistorySearchListBox.SelectedItem != null)
                        PromptHistorySearchListBox.ScrollIntoView(PromptHistorySearchListBox.SelectedItem);
                    e.Handled = true;
                    return;
                }
                var isRightArrowCompletion = IsRightArrowCompletionGesture(e);
                if (e.Key is Key.Enter or Key.Tab || isRightArrowCompletion)
                {
                    var completed = promptHistoryViewModel.TryCompletePromptHistorySearch();
                    if (completed)
                        MovePromptCaretToEnd();
                    if (e.Key is Key.Enter or Key.Tab || completed)
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.R
                && Keyboard.Modifiers == ModifierKeys.Control
                && DataContext is CopilotChatViewModel openHistoryViewModel)
            {
                if (openHistoryViewModel.IsPromptHistorySearchOpen
                    || openHistoryViewModel.TryOpenPromptHistorySearch())
                {
                    MovePromptCaretToEnd();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.S
                && Keyboard.Modifiers == ModifierKeys.Control
                && DataContext is CopilotChatViewModel stashViewModel)
            {
                if (stashViewModel.TryToggleComposerStash(
                    PromptTextBox.CaretIndex,
                    out var restoredCaretIndex))
                {
                    ApplyPromptCaret(restoredCaretIndex);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = OpenExpandedPromptEditor();
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.None
                && DataContext is CopilotChatViewModel referenceViewModel
                && referenceViewModel.IsComposerReferenceMentionActive)
            {
                if (e.Key == Key.Escape)
                {
                    referenceViewModel.DismissComposerReferenceSuggestions();
                    e.Handled = true;
                    return;
                }
                if (e.Key is Key.Up or Key.Down
                    && referenceViewModel.TryNavigateComposerReference(previous: e.Key == Key.Up))
                {
                    e.Handled = true;
                    return;
                }
                var isRightArrowCompletion = IsRightArrowCompletionGesture(e);
                if (e.Key is Key.Enter or Key.Tab || isRightArrowCompletion)
                {
                    if (referenceViewModel.HasComposerReferenceSuggestions
                        && referenceViewModel.TryCompleteComposerReference())
                    {
                        MovePromptCaretToEnd();
                        e.Handled = true;
                        return;
                    }

                    if (!isRightArrowCompletion
                        && CopilotComposerReferenceCatalog.ShouldConsumeReferenceCompletionKey(
                            e.Key == Key.Tab,
                            referenceViewModel.HasComposerReferenceSuggestions,
                            referenceViewModel.IsComposerReferenceSearchPending))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (Keyboard.Modifiers == ModifierKeys.None
                && e.Key is Key.Up or Key.Down
                && DataContext is CopilotChatViewModel commandViewModel
                && commandViewModel.TryNavigateLocalCommandSuggestion(previous: e.Key == Key.Up))
            {
                LocalCommandSuggestionListBox.SelectedIndex =
                    commandViewModel.SelectedLocalCommandSuggestionIndex;
                if (LocalCommandSuggestionListBox.SelectedItem != null)
                    LocalCommandSuggestionListBox.ScrollIntoView(LocalCommandSuggestionListBox.SelectedItem);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.None
                && e.Key is Key.Up or Key.Down
                && DataContext is CopilotChatViewModel historyViewModel
                && (historyViewModel.IsInputEmpty || historyViewModel.IsNavigatingPromptHistory)
                && historyViewModel.TryNavigatePromptHistory(previous: e.Key == Key.Up))
            {
                MovePromptCaretToEnd();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.PageUp or Key.PageDown
                && TryPageConversationFromPrompt(e.Key, Keyboard.Modifiers))
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (DataContext is CopilotChatViewModel pasteViewModel
                    && pasteViewModel.TryBeginPasteClipboardImageAttachment(out var operation))
                {
                    e.Handled = true;
                    await operation;
                    return;
                }
            }

            if (DataContext is CopilotChatViewModel promptCompletionViewModel
                && IsRightArrowCompletionGesture(e)
                && promptCompletionViewModel.TryAcceptPromptHistoryPrefixCompletion())
            {
                MovePromptCaretToEnd();
                e.Handled = true;
                return;
            }

            if (DataContext is CopilotChatViewModel completionViewModel
                && (e.Key == Key.Tab || IsRightArrowCompletionGesture(e))
                && completionViewModel.TryCompleteLocalCommand())
            {
                MovePromptCaretToEnd();
                e.Handled = true;
                return;
            }

            if (DataContext is CopilotChatViewModel queueViewModel
                && e.Key == Key.Tab
                && Keyboard.Modifiers == ModifierKeys.None
                && queueViewModel.TrySubmitAlternateCurrentRunFollowUp())
            {
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter || DataContext is not CopilotChatViewModel viewModel)
                return;

            var modifiers = Keyboard.Modifiers;
            var enterAction = CopilotMultilineComposerPreference.ResolveEnterAction(
                viewModel.UseMultilineComposer,
                (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift,
                (modifiers & ModifierKeys.Control) == ModifierKeys.Control);
            var commandSuggestionConsumesEnter =
                modifiers == ModifierKeys.None && viewModel.HasLocalCommandSuggestions;
            if (enterAction == CopilotComposerEnterAction.InsertLine
                && !commandSuggestionConsumesEnter)
            {
                return;
            }

            if (!viewModel.TryCompleteLocalCommandForSubmission())
            {
                e.Handled = true;
                return;
            }
            if (modifiers == ModifierKeys.Control
                && viewModel.CanSteerCurrentRun)
            {
                viewModel.TrySendCurrentRunFollowUpNow();
                e.Handled = true;
                return;
            }
            viewModel.SendCommand.Execute(null);

            e.Handled = true;
        }

        private bool TryPageConversationFromPrompt(Key key, ModifierKeys modifiers)
        {
            if (DataContext is not CopilotChatViewModel viewModel)
                return false;

            var messagesScrollViewer = GetMessagesScrollViewer();
            if (messagesScrollViewer == null)
                return false;

            var promptScrollViewer = FindVisualChild<ScrollViewer>(PromptTextBox);
            var hasComposerOverlay = viewModel.IsPromptHistorySearchOpen
                || viewModel.IsComposerReferenceMentionActive
                || viewModel.HasLocalCommandSuggestions;
            if (!ShouldPageConversation(
                key,
                modifiers,
                hasComposerOverlay,
                PromptTextBox.GetLineIndexFromCharacterIndex(PromptTextBox.CaretIndex),
                PromptTextBox.LineCount,
                PromptTextBox.SelectionLength,
                promptScrollViewer?.VerticalOffset ?? 0,
                promptScrollViewer?.ScrollableHeight ?? 0,
                messagesScrollViewer.VerticalOffset,
                messagesScrollViewer.ScrollableHeight))
            {
                return false;
            }

            if (key == Key.PageUp)
                messagesScrollViewer.PageUp();
            else
                messagesScrollViewer.PageDown();
            return true;
        }

        internal static bool ShouldPageConversation(
            Key key,
            ModifierKeys modifiers,
            bool hasComposerOverlay,
            int promptCaretLineIndex,
            int promptLineCount,
            int promptSelectionLength,
            double promptVerticalOffset,
            double promptScrollableHeight,
            double conversationVerticalOffset,
            double conversationScrollableHeight)
        {
            const double boundaryTolerance = 0.5;
            if (key is not (Key.PageUp or Key.PageDown)
                || modifiers != ModifierKeys.None
                || hasComposerOverlay
                || promptSelectionLength != 0)
            {
                return false;
            }

            if (promptLineCount > 1
                && (promptCaretLineIndex < 0
                    || (key == Key.PageUp && promptCaretLineIndex > 0)
                    || (key == Key.PageDown && promptCaretLineIndex < promptLineCount - 1)))
            {
                return false;
            }

            var promptCanScroll = key == Key.PageUp
                ? promptVerticalOffset > boundaryTolerance
                : promptScrollableHeight - promptVerticalOffset > boundaryTolerance;
            if (promptCanScroll)
                return false;

            return key == Key.PageUp
                ? conversationVerticalOffset > boundaryTolerance
                : conversationScrollableHeight - conversationVerticalOffset > boundaryTolerance;
        }

        private bool IsRightArrowCompletionGesture(KeyEventArgs e)
        {
            return e.Key == Key.Right
                && Keyboard.Modifiers == ModifierKeys.None
                && CopilotComposerCompletionKeys.CanAcceptRightArrow(
                    PromptTextBox.CaretIndex,
                    PromptTextBox.SelectionLength,
                    PromptTextBox.Text.Length);
        }

        private void LocalCommandSuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            FocusPromptInput();
        }

        private void ComposerReferenceSuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            FocusPromptInput();
        }

        private void PromptHistorySearchResultButton_Click(object sender, RoutedEventArgs e)
        {
            FocusPromptInput();
            MovePromptCaretToEnd();
        }

        private void PromptHistoryPrefixCompletionButton_Click(object sender, RoutedEventArgs e)
        {
            FocusPromptInput();
            MovePromptCaretToEnd();
        }

        private void ComposerStashButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CopilotChatViewModel viewModel
                || !viewModel.TryToggleComposerStash(
                    PromptTextBox.CaretIndex,
                    out var restoredCaretIndex))
            {
                return;
            }

            FocusPromptInput(restoredCaretIndex);
        }

        private void OpenPromptEditorButton_Click(object sender, RoutedEventArgs e)
        {
            OpenExpandedPromptEditor();
        }

        private bool OpenExpandedPromptEditor()
        {
            if (DataContext is not CopilotChatViewModel viewModel
                || !viewModel.CanOpenExpandedComposerEditor)
            {
                return false;
            }

            var initialCaretIndex = PromptTextBox.CaretIndex;
            var window = new CopilotTextInputWindow(
                "编辑 Copilot 提示词",
                "编辑当前未发送内容；Ctrl+Enter 保存，Esc 或取消保持原内容。附件和请求模式不会改变。",
                viewModel.InputText,
                isMultiline: true,
                maximumLength: viewModel.ComposerMaximumCharacters,
                initialCaretIndex: initialCaretIndex,
                acceptsTab: true)
            {
                Width = 720,
                Height = 480,
                MinWidth = 520,
                MinHeight = 320,
                Owner = Window.GetWindow(this) ?? Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true)
            {
                FocusPromptInput(initialCaretIndex);
                return true;
            }

            var snapshot = CopilotComposerEditorSnapshot.Capture(
                window.RawResultText,
                window.ResultCaretIndex);
            viewModel.InputText = snapshot.Text;
            FocusPromptInput(snapshot.CaretIndex);
            return true;
        }

        private void EditMessageButton_Click(object sender, RoutedEventArgs e)
        {
            FocusPromptInput();
        }

        private void ApplyPromptCaret(int caretIndex)
        {
            PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            PromptTextBox.CaretIndex = caretIndex < 0
                ? PromptTextBox.Text.Length
                : Math.Clamp(caretIndex, 0, PromptTextBox.Text.Length);
        }

        private void MovePromptCaretToEnd()
        {
            ApplyPromptCaret(-1);
        }


    }
}
