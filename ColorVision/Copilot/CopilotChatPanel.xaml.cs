using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotChatPanel : UserControl
    {
        private const double CompactSidebarThreshold = 960;
        private const double ExpandedSidebarWidth = 220;
        private const double CollapsedSidebarWidth = 48;

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
        }
    }
}
