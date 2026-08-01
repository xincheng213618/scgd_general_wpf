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
    public partial class CopilotChatPanel : UserControl
    {
        private const double CompactSidebarThreshold = 960;
        private const double CompactComposerThreshold = 560;
        private const double ExpandedSidebarWidth = 232;
        private const double ProfileSelectorPopupMainWidth = 230;
        private const double ProfileSelectorPopupSubmenuWidth = 284;
        private const double ProfileSelectorPopupShadowInset = 14;
        private const byte VirtualKeyLeftWindows = 0x5B;
        private const byte VirtualKeyH = 0x48;
        private const uint KeyEventKeyUp = 0x0002;

        private CopilotChatViewModel? _attachedViewModel;
        private ObservableCollection<CopilotChatMessage>? _attachedMessages;
        private readonly HashSet<CopilotChatMessage> _attachedMessageItems = new();
        private readonly CopilotDoubleEscapeGesture _rewindEscapeGesture = new();
        private ScrollViewer? _messagesScrollViewer;
        private bool _isCompactSidebar;
        private bool _isConversationSidebarExpanded = true;
        private bool _hasConversationSearchPreviewSelection;
        private bool _isScrollToBottomPending;
        private bool _isThemeSubscriptionActive;

        public CopilotChatPanel()
        {
            InitializeComponent();
            BindPromptCaretToThemeResource(PromptTextBox);
            DataContextChanged += CopilotChatPanel_DataContextChanged;
            Loaded += CopilotChatPanel_Loaded;
            PreviewKeyDown += CopilotChatPanel_PreviewKeyDown;
            SizeChanged += CopilotChatPanel_SizeChanged;
            Unloaded += CopilotChatPanel_Unloaded;
            DataObject.AddPastingHandler(PromptTextBox, PromptTextBox_Pasting);
        }

        private void CopilotChatPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isThemeSubscriptionActive)
            {
                ThemeManager.Current.CurrentUIThemeChanged += ThemeManager_CurrentUIThemeChanged;
                _isThemeSubscriptionActive = true;
            }

            SchedulePromptCaretBrushRefresh(ThemeManager.Current.CurrentUITheme);
            AttachViewModel(DataContext as CopilotChatViewModel);
            UpdateResponsiveLayout();
        }

        private static void BindPromptCaretToThemeResource(TextBox promptTextBox)
        {
            promptTextBox.SetResourceReference(TextBoxBase.CaretBrushProperty, "GlobalTextBrush");
        }

        private void ThemeManager_CurrentUIThemeChanged(Theme theme)
        {
            SchedulePromptCaretBrushRefresh(theme);
        }

        private void SchedulePromptCaretBrushRefresh(Theme theme)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, () => ApplyPromptCaretBrush(PromptTextBox, theme));
        }

        private static void ApplyPromptCaretBrush(TextBox promptTextBox, Theme theme)
        {
            promptTextBox.CaretBrush = theme == Theme.Dark ? Brushes.White : Brushes.Black;
            promptTextBox.InvalidateVisual();
        }

        private void PromptTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ApplyPromptCaretBrush(PromptTextBox, ThemeManager.Current.CurrentUITheme);
        }

        private void CopilotChatPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var isPlainEscape = key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None;
            if (!isPlainEscape)
                _rewindEscapeGesture.Reset();
            var showRewindPoints = isPlainEscape
                && _rewindEscapeGesture.Register(DateTimeOffset.UtcNow);
            if (key is Key.Oem2 or Key.Divide
                && Keyboard.Modifiers == ModifierKeys.Control
                && DataContext is CopilotChatViewModel shortcutViewModel)
            {
                shortcutViewModel.ShowKeyboardShortcutHelp();
                e.Handled = true;
                return;
            }

            if (key == Key.P && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (OpenProfileSelector())
                    e.Handled = true;
                return;
            }

            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is CopilotChatViewModel openFindViewModel)
                {
                    openFindViewModel.OpenConversationFind();
                    FocusConversationFind();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
            {
                FocusConversationSearch();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is CopilotChatViewModel viewModel && viewModel.NewChatCommand.CanExecute(null))
                {
                    CloseProfileSelectorPopup();
                    viewModel.NewChatCommand.Execute(null);
                    FocusPromptInput();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is CopilotChatViewModel viewModel
                    && viewModel.CopyLatestResponseCommand.CanExecute(null))
                {
                    viewModel.CopyLatestResponseCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is CopilotChatViewModel viewModel
                    && viewModel.ToggleAgentTaskPanelCommand.CanExecute(null))
                {
                    viewModel.ToggleAgentTaskPanelCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key != Key.Escape)
                return;

            if (ProfileSelectorPopup.IsOpen)
            {
                _rewindEscapeGesture.Reset();
                CloseProfileSelectorPopup();
                e.Handled = true;
                return;
            }

            if (ConversationFindTextBox.IsKeyboardFocusWithin
                && DataContext is CopilotChatViewModel closeFindViewModel
                && closeFindViewModel.CloseConversationFindCommand.CanExecute(null))
            {
                _rewindEscapeGesture.Reset();
                closeFindViewModel.CloseConversationFindCommand.Execute(null);
                FocusPromptInput();
                e.Handled = true;
                return;
            }

            if (ConversationSearchTextBox.IsKeyboardFocusWithin
                || (ConversationListBox.IsKeyboardFocusWithin
                    && DataContext is CopilotChatViewModel focusedSearchViewModel
                    && focusedSearchViewModel.HasConversationSearchQuery))
            {
                _rewindEscapeGesture.Reset();
                if (DataContext is CopilotChatViewModel searchViewModel
                    && searchViewModel.ClearConversationSearchCommand.CanExecute(null))
                {
                    searchViewModel.ClearConversationSearchCommand.Execute(null);
                }
                FocusPromptInput();
                e.Handled = true;
                return;
            }

            if (DataContext is CopilotChatViewModel historyViewModel
                && historyViewModel.CancelPromptHistoryNavigation())
            {
                _rewindEscapeGesture.Reset();
                FocusPromptInput();
                e.Handled = true;
                return;
            }

            if (DataContext is CopilotChatViewModel editViewModel
                && editViewModel.CancelMessageEditCommand.CanExecute(null))
            {
                _rewindEscapeGesture.Reset();
                editViewModel.CancelMessageEditCommand.Execute(null);
                FocusPromptInput();
                e.Handled = true;
                return;
            }

            if (_isCompactSidebar && _isConversationSidebarExpanded)
            {
                _rewindEscapeGesture.Reset();
                _isConversationSidebarExpanded = false;
                UpdateResponsiveLayout();
                FocusPromptInput();
                e.Handled = true;
                return;
            }

            if (PromptTextBox.IsKeyboardFocusWithin
                && DataContext is CopilotChatViewModel composerViewModel
                && (composerViewModel.IsPromptHistorySearchOpen
                    || composerViewModel.IsComposerReferenceMentionActive))
            {
                _rewindEscapeGesture.Reset();
                return;
            }

            if (isPlainEscape
                && DataContext is CopilotChatViewModel escapeViewModel)
            {
                if (escapeViewModel.TryStopCurrentReplyFromKeyboard())
                {
                    _rewindEscapeGesture.Reset();
                    e.Handled = true;
                    return;
                }

                if (escapeViewModel.CanShowConversationRewindShortcut)
                {
                    e.Handled = true;
                    if (showRewindPoints)
                        escapeViewModel.ShowConversationRewindPointsFromKeyboard();
                    return;
                }
            }

            _rewindEscapeGesture.Reset();
        }

        private void FocusConversationFind()
        {
            CloseProfileSelectorPopup();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                ConversationFindTextBox.Focus();
                Keyboard.Focus(ConversationFindTextBox);
                ConversationFindTextBox.SelectAll();
            });
        }

        private void FocusConversationSearch()
        {
            CloseProfileSelectorPopup();
            _hasConversationSearchPreviewSelection = false;
            if (_isCompactSidebar && !_isConversationSidebarExpanded)
            {
                _isConversationSidebarExpanded = true;
                UpdateResponsiveLayout();
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                ConversationSearchTextBox.Focus();
                Keyboard.Focus(ConversationSearchTextBox);
                ConversationSearchTextBox.SelectAll();
            });
        }

        private void FocusPromptInput(int caretIndex = -1)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                PromptTextBox.Focus();
                Keyboard.Focus(PromptTextBox);
                ApplyPromptCaret(caretIndex);
            });
        }

        private void CopilotChatPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout();
        }

        private void CopilotChatPanel_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            _rewindEscapeGesture.Reset();
            DetachViewModel(e.OldValue as CopilotChatViewModel);
            AttachViewModel(e.NewValue as CopilotChatViewModel);
            ScrollToBottom();
        }

        private void CopilotChatPanel_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _rewindEscapeGesture.Reset();
            if (_isThemeSubscriptionActive)
            {
                ThemeManager.Current.CurrentUIThemeChanged -= ThemeManager_CurrentUIThemeChanged;
                _isThemeSubscriptionActive = false;
            }

            CloseProfileSelectorPopup();
            DetachViewModel(DataContext as CopilotChatViewModel);
            _messagesScrollViewer = null;
        }

        private void AttachViewModel(CopilotChatViewModel? viewModel)
        {
            if (viewModel == null)
                return;
            if (ReferenceEquals(_attachedViewModel, viewModel))
            {
                if (!ReferenceEquals(_attachedMessages, viewModel.Messages))
                    ResetMessageSubscriptions(viewModel.Messages);
                return;
            }

            DetachViewModel(_attachedViewModel);

            _attachedViewModel = viewModel;
            viewModel.ConversationSearchRequested += ViewModel_ConversationSearchRequested;
            viewModel.ProfileSelectionRequested += ViewModel_ProfileSelectionRequested;
            viewModel.ReasoningSelectionRequested += ViewModel_ReasoningSelectionRequested;
            viewModel.AccessModeSelectionRequested += ViewModel_AccessModeSelectionRequested;
            viewModel.MessageNavigationRequested += ViewModel_MessageNavigationRequested;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            ResetMessageSubscriptions(viewModel.Messages);
            UpdateEmptyStateVisibility();
        }

        private void DetachViewModel(CopilotChatViewModel? viewModel)
        {
            if (_attachedViewModel == null
                || viewModel != null && !ReferenceEquals(_attachedViewModel, viewModel))
                return;

            _attachedViewModel.ConversationSearchRequested -= ViewModel_ConversationSearchRequested;
            _attachedViewModel.ProfileSelectionRequested -= ViewModel_ProfileSelectionRequested;
            _attachedViewModel.ReasoningSelectionRequested -= ViewModel_ReasoningSelectionRequested;
            _attachedViewModel.AccessModeSelectionRequested -= ViewModel_AccessModeSelectionRequested;
            _attachedViewModel.MessageNavigationRequested -= ViewModel_MessageNavigationRequested;
            _attachedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ResetMessageSubscriptions(null);
            _attachedViewModel = null;
        }

        private void ViewModel_ConversationSearchRequested(object? sender, EventArgs e)
        {
            if (ReferenceEquals(sender, _attachedViewModel))
                FocusConversationSearch();
        }

        private void ViewModel_ProfileSelectionRequested(object? sender, EventArgs e)
        {
            if (ReferenceEquals(sender, _attachedViewModel))
                OpenProfileSelector();
        }

        private void ViewModel_ReasoningSelectionRequested(object? sender, EventArgs e)
        {
            if (ReferenceEquals(sender, _attachedViewModel))
                OpenReasoningSelector();
        }

        private void ViewModel_AccessModeSelectionRequested(object? sender, EventArgs e)
        {
            if (ReferenceEquals(sender, _attachedViewModel))
                OpenAccessModeMenu();
        }

        private void ViewModel_MessageNavigationRequested(
            object? sender,
            CopilotChatMessageNavigationRequestedEventArgs e)
        {
            if (ReferenceEquals(sender, _attachedViewModel))
                ScrollToMessage(e.Message);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_attachedViewModel == null)
                return;

            if (e.PropertyName == nameof(CopilotChatViewModel.Messages))
            {
                ResetMessageSubscriptions(_attachedViewModel.Messages);
                ScrollToBottom();
            }

            if (e.PropertyName == nameof(CopilotChatViewModel.Messages)
                || e.PropertyName == nameof(CopilotChatViewModel.SelectedConversation)
                || e.PropertyName == nameof(CopilotChatViewModel.IsConversationEmpty)
                || e.PropertyName == nameof(CopilotChatViewModel.CanShowCompactHistory))
            {
                UpdateEmptyStateVisibility();
            }

            if (e.PropertyName == nameof(CopilotChatViewModel.IsConversationFindOpen)
                && _attachedViewModel.IsConversationFindOpen)
            {
                FocusConversationFind();
            }

            if (e.PropertyName == nameof(CopilotChatViewModel.CurrentConversationFindMatch)
                && _attachedViewModel.CurrentConversationFindMatch is { } match)
            {
                ScrollToMessage(match);
            }
        }

        private void ScrollToMessage(CopilotChatMessage message)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (_attachedViewModel?.Messages.Contains(message) != true)
                    return;

                MessagesListBox.ScrollIntoView(message);
                if (MessagesListBox.ItemContainerGenerator.ContainerFromItem(message) is FrameworkElement container)
                    container.BringIntoView();
            });
        }

        private void ResetMessageSubscriptions(ObservableCollection<CopilotChatMessage>? messages)
        {
            if (_attachedMessages != null)
                _attachedMessages.CollectionChanged -= Messages_CollectionChanged;

            foreach (var message in _attachedMessageItems)
                message.PropertyChanged -= Message_PropertyChanged;
            _attachedMessageItems.Clear();

            _attachedMessages = messages;
            if (_attachedMessages != null)
                _attachedMessages.CollectionChanged += Messages_CollectionChanged;

            SynchronizeMessageSubscriptions();
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var shouldFollowNewMessage = _isScrollToBottomPending
                || IsNearBottom()
                || e.NewItems?.OfType<CopilotChatMessage>().Any(message => message.IsUser) == true;
            SynchronizeMessageSubscriptions();
            if (shouldFollowNewMessage)
                ScrollToBottom();
            else if (e.Action == NotifyCollectionChangedAction.Add)
                ShowScrollToLatestButton();
            UpdateEmptyStateVisibility();
        }

        private void SynchronizeMessageSubscriptions()
        {
            var currentMessages = _attachedMessages == null
                ? new HashSet<CopilotChatMessage>()
                : new HashSet<CopilotChatMessage>(_attachedMessages);
            foreach (var message in _attachedMessageItems)
            {
                if (!currentMessages.Contains(message))
                    message.PropertyChanged -= Message_PropertyChanged;
            }

            _attachedMessageItems.RemoveWhere(message => !currentMessages.Contains(message));
            foreach (var message in currentMessages)
            {
                if (_attachedMessageItems.Add(message))
                    message.PropertyChanged += Message_PropertyChanged;
            }
        }

        private void Message_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CopilotChatMessage.Content)
                || e.PropertyName == nameof(CopilotChatMessage.ExecutionContent)
                || e.PropertyName == nameof(CopilotChatMessage.ReasoningContent))
            {
                if (_attachedViewModel?.IsConversationFindOpen == true)
                    _attachedViewModel.RefreshConversationFind();

                if (_isScrollToBottomPending || IsNearBottom())
                    ScrollToBottom();
                else
                    ShowScrollToLatestButton();
            }
        }

        private void AttachMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.ContextMenu == null)
                return;

            element.ContextMenu.PlacementTarget = element;
            element.ContextMenu.Placement = PlacementMode.Top;
            element.ContextMenu.IsOpen = true;
        }

        private void ConversationBranchFamilyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.ContextMenu == null)
                return;

            element.ContextMenu.PlacementTarget = element;
            element.ContextMenu.Placement = PlacementMode.Bottom;
            element.ContextMenu.IsOpen = true;
        }

        private void ConversationBranchFamilyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CopilotChatViewModel viewModel
                || sender is not MenuItem { DataContext: CopilotConversationBranchFamilyMember member }
                || !viewModel.SelectConversationCommand.CanExecute(member.Conversation))
            {
                return;
            }

            viewModel.SelectConversationCommand.Execute(member.Conversation);
        }

        private void ComposerReferenceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CopilotChatViewModel viewModel)
                return;

            var updated = CopilotComposerReferenceCatalog.InsertMention(
                viewModel.InputText,
                PromptTextBox.SelectionStart,
                PromptTextBox.SelectionLength,
                out var caretIndex);
            viewModel.InputText = updated;
            PromptTextBox.Focus();
            Keyboard.Focus(PromptTextBox);
            PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            PromptTextBox.CaretIndex = Math.Clamp(caretIndex, 0, PromptTextBox.Text.Length);
        }

        private void AccessModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, AccessModeButton))
                OpenAccessModeMenu();
        }

        private void OpenAccessModeMenu()
        {
            if (AccessModeButton.ContextMenu == null)
                return;

            AccessModeButton.ContextMenu.PlacementTarget = AccessModeButton;
            AccessModeButton.ContextMenu.Placement = PlacementMode.Top;
            AccessModeButton.ContextMenu.IsOpen = true;
        }

        private void ProfileSelectorPopup_Opened(object sender, EventArgs e)
        {
            SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: false);
        }

        private void ProfileSelectorPopup_Closed(object sender, EventArgs e)
        {
            ProfileSelectorButton.IsChecked = false;
            SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: false);
        }

        private void ModelSelectorRowButton_Click(object sender, RoutedEventArgs e)
        {
            SetProfileSelectorSubmenu(modelVisible: ModelSelectorRowButton.IsChecked == true, reasoningVisible: false);
        }

        private void ReasoningSelectorRowButton_Click(object sender, RoutedEventArgs e)
        {
            SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: ReasoningSelectorRowButton.IsChecked == true);
        }

        private void ProfileListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source
                || ItemsControl.ContainerFromElement(ProfileListBox, source) is not ListBoxItem)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(CloseProfileSelectorPopup), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ReasoningOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CopilotReasoningMode mode }
                && DataContext is CopilotChatViewModel viewModel)
            {
                viewModel.SetSelectedProfileReasoningMode(mode);
            }

            CloseProfileSelectorPopup();
        }

        private void CloseSelectorPopupButton_Click(object sender, RoutedEventArgs e)
        {
            CloseProfileSelectorPopup();
        }

        private void SetProfileSelectorSubmenu(bool modelVisible, bool reasoningVisible)
        {
            ModelSelectorRowButton.IsChecked = modelVisible;
            ReasoningSelectorRowButton.IsChecked = reasoningVisible;
            ModelSubmenuBorder.Visibility = modelVisible ? Visibility.Visible : Visibility.Collapsed;
            ReasoningSubmenuBorder.Visibility = reasoningVisible ? Visibility.Visible : Visibility.Collapsed;
            var popupWidth = ProfileSelectorPopupMainWidth + (modelVisible || reasoningVisible ? ProfileSelectorPopupSubmenuWidth : 0);
            ProfileSelectorPopup.HorizontalOffset = ProfileSelectorButton.ActualWidth - popupWidth - ProfileSelectorPopupShadowInset;
        }

        private bool OpenProfileSelector()
        {
            if (DataContext is not CopilotChatViewModel viewModel || !viewModel.CanSelectProfile)
                return false;

            ProfileSelectorButton.IsChecked = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!ProfileSelectorPopup.IsOpen)
                    return;

                SetProfileSelectorSubmenu(modelVisible: true, reasoningVisible: false);
                ProfileListBox.Focus();
                Keyboard.Focus(ProfileListBox);
                if (viewModel.SelectedProfile != null)
                    ProfileListBox.ScrollIntoView(viewModel.SelectedProfile);
            });
            return true;
        }

        private bool OpenReasoningSelector()
        {
            if (DataContext is not CopilotChatViewModel viewModel
                || !viewModel.CanSelectProfile
                || !viewModel.HasConfigurableReasoning)
            {
                return false;
            }

            ProfileSelectorButton.IsChecked = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!ProfileSelectorPopup.IsOpen)
                    return;

                SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: true);
                ReasoningOptionsControl.UpdateLayout();
                var selectedIndex = ReasoningOptionsControl.Items
                    .Cast<CopilotReasoningOption>()
                    .Select((option, index) => (option, index))
                    .FirstOrDefault(item => item.option.IsSelected)
                    .index;
                var container = ReasoningOptionsControl.ItemContainerGenerator.ContainerFromIndex(selectedIndex);
                var button = container == null ? null : FindVisualChild<Button>(container);
                if (button != null)
                {
                    button.Focus();
                    Keyboard.Focus(button);
                }
                else
                {
                    ReasoningSelectorRowButton.Focus();
                    Keyboard.Focus(ReasoningSelectorRowButton);
                }
            });
            return true;
        }

        private void CloseProfileSelectorPopup()
        {
            if (ProfileSelectorPopup == null)
                return;

            ProfileSelectorPopup.IsOpen = false;
        }

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
            double promptVerticalOffset,
            double promptScrollableHeight,
            double conversationVerticalOffset,
            double conversationScrollableHeight)
        {
            const double boundaryTolerance = 0.5;
            if (key is not (Key.PageUp or Key.PageDown)
                || modifiers != ModifierKeys.None
                || hasComposerOverlay)
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

        private void ScrollToLatestButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToBottom();
        }


        private void UpdateResponsiveLayout()
        {
            var isCompact = ActualWidth > 0 && ActualWidth < CompactSidebarThreshold;
            if (isCompact && !_isCompactSidebar)
                _isConversationSidebarExpanded = false;

            if (!isCompact)
                _isConversationSidebarExpanded = true;

            _isCompactSidebar = isCompact;

            var showCollapsedStrip = _isCompactSidebar && !_isConversationSidebarExpanded;
            SidebarColumnDefinition.Width = new GridLength(showCollapsedStrip ? 0 : ExpandedSidebarWidth);
            ConversationSidebarBorder.Visibility = showCollapsedStrip ? Visibility.Collapsed : Visibility.Visible;
            TitleBarConversationButton.Visibility = showCollapsedStrip ? Visibility.Visible : Visibility.Collapsed;
            CompactSidebarToggleButton.Visibility = _isCompactSidebar && !showCollapsedStrip ? Visibility.Visible : Visibility.Collapsed;

            var isCompactComposer = ActualWidth > 0 && ActualWidth < CompactComposerThreshold;
            ComposerShellBorder.Margin = isCompactComposer ? new Thickness(10, 0, 10, 10) : new Thickness(24, 0, 24, 14);
            ComposerSelectorGrid.MaxWidth = isCompactComposer ? 132 : 180;
            ProfileSelectorButton.MaxWidth = isCompactComposer ? 132 : 180;
            ProfileSelectorButton.Padding = isCompactComposer ? new Thickness(2, 0, 0, 0) : new Thickness(4, 0, 2, 0);
            AccessModeLabelTextBlock.Visibility = isCompactComposer ? Visibility.Collapsed : Visibility.Visible;

            UpdateEmptyStateVisibility();
        }

        private void UpdateEmptyStateVisibility()
        {
            if (DataContext is not CopilotChatViewModel viewModel)
            {
                CompactHistoryPanel.Visibility = Visibility.Collapsed;
                EmptyStateTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            var showCompactHistory = CopilotResponsiveLayout.ShouldShowCompactHistory(
                _isCompactSidebar,
                _isConversationSidebarExpanded,
                viewModel.IsConversationEmpty,
                viewModel.CanShowCompactHistory);
            CompactHistoryPanel.Visibility = showCompactHistory ? Visibility.Visible : Visibility.Collapsed;
            EmptyStateTextBlock.Visibility = viewModel.IsConversationEmpty && !showCompactHistory ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void SendWindowsVoiceTypingShortcut()
        {
            keybd_event(VirtualKeyLeftWindows, 0, 0, UIntPtr.Zero);
            keybd_event(VirtualKeyH, 0, 0, UIntPtr.Zero);
            keybd_event(VirtualKeyH, 0, KeyEventKeyUp, UIntPtr.Zero);
            keybd_event(VirtualKeyLeftWindows, 0, KeyEventKeyUp, UIntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
