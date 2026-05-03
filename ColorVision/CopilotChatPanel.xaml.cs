using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{
    public partial class CopilotChatPanel : UserControl
    {
        public CopilotChatPanel()
        {
            InitializeComponent();
            DataContextChanged += CopilotChatPanel_DataContextChanged;
            Unloaded += CopilotChatPanel_Unloaded;
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

            viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            foreach (var message in viewModel.Messages)
            {
                message.PropertyChanged += Message_PropertyChanged;
            }
        }

        private void DetachViewModel(CopilotChatViewModel? viewModel)
        {
            if (viewModel == null)
                return;

            viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            foreach (var message in viewModel.Messages)
            {
                message.PropertyChanged -= Message_PropertyChanged;
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
            if (e.PropertyName == nameof(CopilotChatMessage.Content))
                ScrollToBottom();
        }

        private void PromptTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;

            if (DataContext is CopilotChatViewModel viewModel)
                viewModel.SendCommand.Execute(null);

            e.Handled = true;
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(() => MessagesScrollViewer.ScrollToEnd());
        }
    }
}