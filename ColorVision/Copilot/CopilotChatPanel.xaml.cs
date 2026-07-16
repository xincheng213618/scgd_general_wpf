using ColorVision.Themes;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System;
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
        private const double CollapsedSidebarWidth = 48;
        private const double ProfileSelectorPopupMainWidth = 230;
        private const double ProfileSelectorPopupSubmenuWidth = 284;
        private const double ProfileSelectorPopupShadowInset = 14;
        private const byte VirtualKeyLeftWindows = 0x5B;
        private const byte VirtualKeyH = 0x48;
        private const uint KeyEventKeyUp = 0x0002;

        private CopilotChatViewModel? _attachedViewModel;
        private ObservableCollection<CopilotChatMessage>? _attachedMessages;
        private readonly HashSet<CopilotChatMessage> _attachedMessageItems = new();
        private bool _isCompactSidebar;
        private bool _isConversationSidebarExpanded = true;
        private bool _isThemeSubscriptionActive;

        public CopilotChatPanel()
        {
            InitializeComponent();
            BindPromptCaretToThemeResource(PromptTextBox);
            DataContextChanged += CopilotChatPanel_DataContextChanged;
            Loaded += CopilotChatPanel_Loaded;
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

        private void CopilotChatPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout();
        }

        private void CopilotChatPanel_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            DetachViewModel(e.OldValue as CopilotChatViewModel);
            AttachViewModel(e.NewValue as CopilotChatViewModel);
            ScrollToBottom();
        }

        private void CopilotChatPanel_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_isThemeSubscriptionActive)
            {
                ThemeManager.Current.CurrentUIThemeChanged -= ThemeManager_CurrentUIThemeChanged;
                _isThemeSubscriptionActive = false;
            }

            CloseProfileSelectorPopup();
            DetachViewModel(DataContext as CopilotChatViewModel);
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
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            ResetMessageSubscriptions(viewModel.Messages);
            UpdateEmptyStateVisibility();
        }

        private void DetachViewModel(CopilotChatViewModel? viewModel)
        {
            if (_attachedViewModel == null
                || viewModel != null && !ReferenceEquals(_attachedViewModel, viewModel))
                return;

            _attachedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ResetMessageSubscriptions(null);
            _attachedViewModel = null;
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
            SynchronizeMessageSubscriptions();
            ScrollToBottom();
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
                ScrollToBottom(forceIfNearBottomOnly: true);
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

        private void PromptTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (DataContext is CopilotChatViewModel pasteViewModel && pasteViewModel.TryPasteClipboardImageAttachment())
                {
                    e.Handled = true;
                    return;
                }
            }

            if (DataContext is CopilotChatViewModel completionViewModel
                && e.Key == Key.Tab
                && completionViewModel.TryCompleteLocalCommand())
            {
                MovePromptCaretToEnd();
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;

            if (DataContext is CopilotChatViewModel viewModel)
            {
                viewModel.TryCompleteLocalCommand();
                viewModel.SendCommand.Execute(null);
            }

            e.Handled = true;
        }

        private void LocalCommandSuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                PromptTextBox.Focus();
                Keyboard.Focus(PromptTextBox);
                MovePromptCaretToEnd();
            });
        }

        private void MovePromptCaretToEnd()
        {
            PromptTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
        }

        private void PromptTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (DataContext is not CopilotChatViewModel viewModel)
                return;

            if (!e.SourceDataObject.GetDataPresent(DataFormats.Bitmap) && !Clipboard.ContainsImage())
                return;

            if (!viewModel.TryPasteClipboardImageAttachment())
                return;

            e.CancelCommand();
        }

        private bool IsNearBottom()
        {
            const double threshold = 36;
            return MessagesScrollViewer.ScrollableHeight - MessagesScrollViewer.VerticalOffset <= threshold;
        }

        private void ScrollToBottom(bool forceIfNearBottomOnly = false)
        {
            if (forceIfNearBottomOnly && !IsNearBottom())
                return;

            Dispatcher.BeginInvoke(() => MessagesScrollViewer.ScrollToEnd(), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ToggleConversationSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            _isConversationSidebarExpanded = !_isConversationSidebarExpanded;
            UpdateResponsiveLayout();
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

            CurrentLiveContextSummaryText.Visibility = isCompactComposer ? Visibility.Collapsed : Visibility.Visible;
            CurrentLiveContextBorder.Padding = isCompactComposer ? new Thickness(8, 6, 8, 6) : new Thickness(10, 7, 10, 7);
            CurrentLiveContextActionButton.Padding = isCompactComposer ? new Thickness(8, 3, 8, 3) : new Thickness(10, 4, 10, 4);

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
