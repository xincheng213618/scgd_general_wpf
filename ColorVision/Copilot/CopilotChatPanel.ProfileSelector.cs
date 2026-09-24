using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatPanel
    {
        private void ProfileSelectorPopup_Opened(object sender, EventArgs e)
        {
            ProfileSelectorPopup.HorizontalOffset = ProfileSelectorButton.ActualWidth - 328;
        }

        private void ProfileSelectorPopup_Closed(object sender, EventArgs e)
        {
            ProfileSelectorButton.IsChecked = false;
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

        private bool OpenProfileSelector()
        {
            if (DataContext is not CopilotChatViewModel viewModel || !viewModel.CanSelectProfile)
                return false;

            ProfileSelectorButton.IsChecked = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!ProfileSelectorPopup.IsOpen)
                    return;

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
                    ProfileListBox.Focus();
                    Keyboard.Focus(ProfileListBox);
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
    }
}
