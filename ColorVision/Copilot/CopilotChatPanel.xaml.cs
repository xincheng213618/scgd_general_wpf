using System.Collections.Specialized;
using System.ComponentModel;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotChatPanel : UserControl
    {
        private const double CompactSidebarThreshold = 960;
        private const double CompactComposerThreshold = 560;
        private const double ExpandedSidebarWidth = 232;
        private const double CollapsedSidebarWidth = 48;
        private const byte VirtualKeyLeftWindows = 0x5B;
        private const byte VirtualKeyH = 0x48;
        private const uint KeyEventKeyUp = 0x0002;

        private CopilotChatViewModel? _attachedViewModel;
        private INotifyCollectionChanged? _attachedMessages;
        private bool _isCompactSidebar;
        private bool _isConversationSidebarExpanded = true;

        public CopilotChatPanel()
        {
            InitializeComponent();
            DataContextChanged += CopilotChatPanel_DataContextChanged;
            Loaded += CopilotChatPanel_Loaded;
            SizeChanged += CopilotChatPanel_SizeChanged;
            Unloaded += CopilotChatPanel_Unloaded;
            DataObject.AddPastingHandler(PromptTextBox, PromptTextBox_Pasting);
        }

        private void CopilotChatPanel_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateResponsiveLayout();
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
            DetachViewModel(DataContext as CopilotChatViewModel);
        }

        private void AttachViewModel(CopilotChatViewModel? viewModel)
        {
            if (viewModel == null)
                return;

            _attachedViewModel = viewModel;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            ResetMessageSubscriptions(viewModel.Messages);
            UpdateEmptyStateVisibility();
        }

        private void DetachViewModel(CopilotChatViewModel? viewModel)
        {
            if (viewModel == null)
                return;

            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _attachedViewModel = null;
            ResetMessageSubscriptions(null);
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

        private void ResetMessageSubscriptions(INotifyCollectionChanged? messages)
        {
            if (_attachedMessages != null)
                _attachedMessages.CollectionChanged -= Messages_CollectionChanged;

            if (_attachedViewModel != null)
            {
                foreach (var message in _attachedViewModel.Messages)
                {
                    message.PropertyChanged -= Message_PropertyChanged;
                }
            }

            _attachedMessages = messages;
            if (_attachedMessages != null)
                _attachedMessages.CollectionChanged += Messages_CollectionChanged;

            if (_attachedViewModel == null)
                return;

            foreach (var message in _attachedViewModel.Messages)
            {
                message.PropertyChanged += Message_PropertyChanged;
            }
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is CopilotChatMessage message)
                        message.PropertyChanged += Message_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is CopilotChatMessage message)
                        message.PropertyChanged -= Message_PropertyChanged;
                }
            }

            ScrollToBottom();
            UpdateEmptyStateVisibility();
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

        private void SettingsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.ContextMenu == null)
                return;

            element.ContextMenu.PlacementTarget = element;
            element.ContextMenu.Placement = PlacementMode.Bottom;
            element.ContextMenu.IsOpen = true;
        }

        private async void VoiceInputButton_Click(object sender, RoutedEventArgs e)
        {
            PromptTextBox.Focus();
            Keyboard.Focus(PromptTextBox);
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

            if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;

            if (DataContext is CopilotChatViewModel viewModel)
                viewModel.SendCommand.Execute(null);

            e.Handled = true;
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
            ComposerFooterSecondRowDefinition.Height = isCompactComposer ? GridLength.Auto : new GridLength(0);
            ComposerShellBorder.Margin = isCompactComposer ? new Thickness(10, 0, 10, 10) : new Thickness(24, 0, 24, 14);

            Grid.SetRow(ComposerSelectorGrid, 0);
            Grid.SetColumn(ComposerSelectorGrid, isCompactComposer ? 0 : 3);
            Grid.SetColumnSpan(ComposerSelectorGrid, isCompactComposer ? 5 : 1);
            ComposerSelectorGrid.Margin = isCompactComposer ? new Thickness(0, 0, 0, 6) : new Thickness(8, 0, 8, 0);

            Grid.SetRow(AttachMenuButton, isCompactComposer ? 1 : 0);
            Grid.SetColumn(AttachMenuButton, 0);

            Grid.SetRow(ControlModePill, isCompactComposer ? 1 : 0);
            Grid.SetColumn(ControlModePill, 1);

            Grid.SetRow(ComposerActionStack, isCompactComposer ? 1 : 0);
            Grid.SetColumn(ComposerActionStack, 4);

            ProfileComboBox.MaxWidth = isCompactComposer ? double.PositiveInfinity : 180;

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

            var showCompactHistory = _isCompactSidebar && viewModel.IsConversationEmpty && viewModel.CanShowCompactHistory;
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
